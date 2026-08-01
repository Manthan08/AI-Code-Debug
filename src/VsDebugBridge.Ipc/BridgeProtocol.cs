using System;
using VsDebugBridge.Contracts;

namespace VsDebugBridge.Ipc
{
    public static class BridgeProtocol
    {
        public static BridgeRequest CreateRequest(string command, object? payload = null)
        {
            return new BridgeRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Command = command,
                PayloadJson = payload == null ? null : BridgeJson.Serialize(payload)
            };
        }

        public static T ReadRequestPayload<T>(BridgeRequest request) where T : new()
        {
            if (string.IsNullOrWhiteSpace(request.PayloadJson))
            {
                return new T();
            }

            return BridgeJson.Deserialize<T>(request.PayloadJson!) ?? new T();
        }

        public static BridgeResponse Success(string requestId, object? payload = null)
        {
            return new BridgeResponse
            {
                RequestId = requestId,
                Success = true,
                PayloadJson = payload == null ? null : BridgeJson.Serialize(payload)
            };
        }

        public static BridgeResponse Failure(string requestId, string code, string message)
        {
            return new BridgeResponse
            {
                RequestId = requestId,
                Success = false,
                Error = new BridgeError
                {
                    Code = code,
                    Message = message
                }
            };
        }

        public static T ReadResponsePayload<T>(BridgeResponse response)
        {
            if (!response.Success)
            {
                var message = response.Error == null ? "Visual Studio bridge request failed." : response.Error.Message;
                throw new InvalidOperationException(message);
            }

            if (string.IsNullOrWhiteSpace(response.PayloadJson))
            {
                throw new InvalidOperationException("Visual Studio bridge returned an empty response payload.");
            }

            return BridgeJson.Deserialize<T>(response.PayloadJson!) ??
                throw new InvalidOperationException("Visual Studio bridge returned an invalid response payload.");
        }
    }
}
