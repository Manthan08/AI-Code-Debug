using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VsDebugBridge.Contracts;

namespace VsDebugBridge.Ipc
{
    public sealed class NamedPipeBridgeServer : IDisposable
    {
        private readonly string _pipeName;
        private readonly Func<BridgeRequest, BridgeResponse> _handleRequest;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _serverTask;

        public NamedPipeBridgeServer(string pipeName, Func<BridgeRequest, BridgeResponse> handleRequest)
        {
            _pipeName = pipeName;
            _handleRequest = handleRequest;
        }

        public void Start()
        {
            if (_serverTask != null)
            {
                return;
            }

            _serverTask = Task.Run(() => Run(_cancellationTokenSource.Token));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            TryUnblockServer();

            try
            {
                _serverTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            _cancellationTokenSource.Dispose();
        }

        private void Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous))
                {
                    try
                    {
                        pipe.WaitForConnectionAsync(cancellationToken).GetAwaiter().GetResult();
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        HandleConnection(pipe);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (IOException)
                    {
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void HandleConnection(Stream pipe)
        {
            using (var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
            using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.AutoFlush = true;
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                BridgeRequest? request = null;
                BridgeResponse response;

                try
                {
                    request = BridgeJson.Deserialize<BridgeRequest>(line!);
                    response = request == null
                        ? BridgeProtocol.Failure(string.Empty, "invalid_request", "Request JSON could not be parsed.")
                        : _handleRequest(request);
                }
                catch (Exception ex)
                {
                    response = BridgeProtocol.Failure(request?.RequestId ?? string.Empty, "handler_error", ex.Message);
                }

                writer.WriteLine(BridgeJson.Serialize(response));
            }
        }

        private void TryUnblockServer()
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out))
                {
                    client.Connect(100);
                }
            }
            catch
            {
            }
        }
    }
}
