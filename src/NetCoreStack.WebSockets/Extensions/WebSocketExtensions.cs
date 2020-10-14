﻿using NetCoreStack.WebSockets.Interfaces;
using NetCoreStack.WebSockets.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NetCoreStack.WebSockets
{
    public static class WebSocketExtensions
    {
        public static WebSocketMessageContext ToContext(this WebSocketReceiveResult result, byte[] input)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            
            var content = Encoding.UTF8.GetString(input, 0, input.Length);
            WebSocketMessageContext webSocketContext = new WebSocketMessageContext();
            try
            {
                webSocketContext = JsonSerializer.Deserialize<WebSocketMessageContext>(content);
            }
            catch (Exception ex)
            {
                webSocketContext.Command = WebSocketCommands.DataSend;
                webSocketContext.Value = content;
                webSocketContext.MessageType = result.MessageType;
            }

            webSocketContext.Length = input.Length;
            return webSocketContext;
        }

        public static async Task<WebSocketMessageContext> ToBinaryContextAsync(this WebSocketReceiveResult result,
            IStreamCompressor compressor,
            byte[] input)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var content = input.Split();
            byte[] header = content.Item1;
            byte[] body = content.Item2;

            var webSocketContext = new WebSocketMessageContext();
            bool isCompressed = GZipHelper.IsGZipBody(body);
            if (isCompressed)
            {
                body = await compressor.DeCompressAsync(body);
            }

            using (var ms = new MemoryStream(header))
            using (var sr = new StreamReader(ms))
            {
                var data = await sr.ReadToEndAsync();
                if (data != null)
                {
                    try
                    {
                        webSocketContext.Header = JsonSerializer.Deserialize<Dictionary<string, object>>(data);
                    }
                    catch (Exception ex)
                    {
                        webSocketContext.Header = new Dictionary<string, object>
                        {
                            ["Exception"] = ex.Message,
                            ["Unknown"] = "Unknown binary message!"
                        };
                    }
                }
            }

            using (var ms = new MemoryStream(body))
            using (var sr = new StreamReader(ms))
            {
                var data = await sr.ReadToEndAsync();
                webSocketContext.Value = data;
            }

            webSocketContext.Length = input.Length;
            webSocketContext.MessageType = WebSocketMessageType.Binary;
            webSocketContext.Command = WebSocketCommands.DataSend;

            return webSocketContext;
        }

        public static ArraySegment<byte> ToSegment(this WebSocketMessageContext webSocketContext)
        {
            if (webSocketContext == null)
            {
                throw new ArgumentNullException(nameof(webSocketContext));
            }

            var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webSocketContext));
            return new ArraySegment<byte>(content, 0, content.Length);
        }

        public static MemoryStream ToMemoryStream(this WebSocketMessageContext webSocketContext)
        {
            if (webSocketContext == null)
            {
                throw new ArgumentNullException(nameof(webSocketContext));
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webSocketContext)));
        }

        public static string GetConnectionId(this WebSocketMessageContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            object connectionId = null;
            if (context.Header.TryGetValue(NCSConstants.ConnectionId, out connectionId))
            {
                return connectionId.ToString();
            }

            throw new ArgumentOutOfRangeException(nameof(connectionId));
        }
    }
}
