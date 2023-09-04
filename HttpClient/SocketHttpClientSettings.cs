using DNS_round_robin.HttpClient.Interfaces;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace DNS_round_robin.HttpClient
{
    public class SocketHttpClientSettings : ISocketHttpClientSettings
    {
        private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ConnectionIdleTimeout = TimeSpan.FromSeconds(1);

        private readonly ConcurrentDictionary<string, int> _indexByHosts = new(StringComparer.OrdinalIgnoreCase);

        public SocketsHttpHandler SocketHttp()
        {
            return new SocketsHttpHandler()
            {
                PooledConnectionLifetime = ConnectionLifetime,
                PooledConnectionIdleTimeout = ConnectionIdleTimeout,
                UseProxy = false,
                UseCookies = false,
                AllowAutoRedirect = false,
                ConnectCallback = CreateConnection
            };
        }

        private async ValueTask<Stream> CreateConnection(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var entryConnection = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.Unspecified, cancellationToken);
            var addresses = ChooseIPAddress(entryConnection);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private IPAddress[] ChooseIPAddress(IPHostEntry entryConnection)
        {
            if (entryConnection.AddressList.Length == 1)
            {
                return entryConnection.AddressList;
            }

            var index = _indexByHosts.AddOrUpdate(
                key: entryConnection.HostName,
                addValue: Random.Shared.Next(),
                updateValueFactory: (host, existingValue) => existingValue + 1) % entryConnection.AddressList.Length;

            if (index == 0)
            {
                return entryConnection.AddressList;
            }

            // Rotate the list of addresses
            var addresses = new IPAddress[entryConnection.AddressList.Length];
            entryConnection.AddressList.AsSpan(index).CopyTo(addresses);
            entryConnection.AddressList.AsSpan(0, index).CopyTo(addresses.AsSpan(index));

            return addresses;
        }
    }

}
