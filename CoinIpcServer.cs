using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace rans0m
{
    public static class CoinIpcServer
    {
        private const string PipeName = "Rans0m_GoldCoinPipe";
        private static CancellationTokenSource? cts;

        public static void Start(Action<string, Point?> onCoinReceived)
        {
            Stop();

            cts = new CancellationTokenSource();
            var token = cts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync(token);

                        using var reader = new StreamReader(server, Encoding.UTF8);
                        string? line = await reader.ReadLineAsync(token);

                        if (!string.IsNullOrEmpty(line))
                        {
                            try
                            {
                                string[] parts = line.Split('|', 3);
                                if (parts.Length == 3 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                                {
                                    onCoinReceived(parts[2].Trim().Trim('"'), new Point(x, y));
                                }
                                else
                                {
                                    onCoinReceived(line.Trim().Trim('"'), null);
                                }
                            }
                            catch { }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch { await Task.Delay(50, token); }
                }
            }, token);
        }

        public static void Stop()
        {
            try
            {
                cts?.Cancel();
                cts?.Dispose();
            }
            catch { }
            cts = null;
        }

        public static bool TrySendToRunningInstance(string coinPath, Point? clickPos = null)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(800); // Wait up to 800ms to see if running instance responds

                using var writer = new StreamWriter(client, Encoding.UTF8);
                if (clickPos.HasValue)
                {
                    writer.WriteLine($"{clickPos.Value.X}|{clickPos.Value.Y}|{coinPath}");
                }
                else
                {
                    writer.WriteLine(coinPath);
                }
                writer.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
