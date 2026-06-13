using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace BetterMultiplayer
{
    public static class NetworkManager
    {
        public static bool IsServer { get; private set; } = false;
        public static bool IsClientConnected { get; private set; } = false;
        public static string RemoteSceneName { get; set; } = "Unknown";
        public static float RemoteNormX { get; set; } = 0f;
        public static float RemoteNormY { get; set; } = 0f;

        private static TcpListener listener;
        private static TcpClient client;
        private static NetworkStream stream;
        private static StreamReader reader;
        private static StreamWriter writer;

        private static Thread serverThread;
        private static Thread clientThread;
        private static bool isRunning = false;

        public static GameObject puppet;

        public static void StartServer(int port)
        {
            if (isRunning) Stop();

            IsServer = true;
            isRunning = true;
            serverThread = new Thread(() => ServerLoop(port)) { IsBackground = true };
            serverThread.Start();
            BetterMultiplayer.Instance.Log("Server thread started on port " + port);
        }

        public static void Connect(string ip, int port)
        {
            if (isRunning) Stop();

            IsServer = false;
            isRunning = true;
            clientThread = new Thread(() => ClientLoop(ip, port)) { IsBackground = true };
            clientThread.Start();
            BetterMultiplayer.Instance.Log("Client thread connecting to " + ip + ":" + port);
        }

        public static void SendPacket(string packet)
        {
            if (writer == null || !IsClientConnected) return;
            try
            {
                lock (writer)
                {
                    writer.WriteLine(packet);
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error sending packet: " + ex);
                Stop();
            }
        }

        public static void Stop()
        {
            if (!isRunning) return;

            isRunning = false;
            IsClientConnected = false;
            IsServer = false;
            RemoteSceneName = "Unknown";

            try { reader?.Close(); } catch {}
            try { writer?.Close(); } catch {}
            try { stream?.Close(); } catch {}
            try { client?.Close(); } catch {}
            try { listener?.Stop(); } catch {}

            reader = null;
            writer = null;
            stream = null;
            client = null;
            listener = null;

            MainThreadDispatcher.Enqueue(() =>
            {
                if (puppet != null)
                {
                    UnityEngine.Object.Destroy(puppet);
                    puppet = null;
                }
            });

            BetterMultiplayer.Instance.Log("Network connection stopped.");
        }

        private static void ServerLoop(int port)
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();

                while (isRunning)
                {
                    TcpClient connectedClient = listener.AcceptTcpClient();
                    if (!isRunning) break;

                    client = connectedClient;
                    stream = client.GetStream();
                    reader = new StreamReader(stream);
                    writer = new StreamWriter(stream);

                    IsClientConnected = true;
                    BetterMultiplayer.Instance.Log("Peer connected to hosted server!");
                    SendPacket($"SKIN|{SkinManager.SelectedSkin}");
                    MainThreadDispatcher.Enqueue(() => ItemSync.SendAllItems());

                    ReadLoop();
                }
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    BetterMultiplayer.Instance.LogError("Server loop error: " + ex);
                }
            }
            finally
            {
                Stop();
            }
        }

        private static void ClientLoop(string ip, int port)
        {
            try
            {
                client = new TcpClient();
                client.Connect(ip, port);
                stream = client.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream);

                IsClientConnected = true;
                BetterMultiplayer.Instance.Log("Connected to host server!");
                SendPacket($"SKIN|{SkinManager.SelectedSkin}");
                MainThreadDispatcher.Enqueue(() => ItemSync.SendAllItems());

                ReadLoop();
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    BetterMultiplayer.Instance.LogError("Client loop error: " + ex);
                }
            }
            finally
            {
                Stop();
            }
        }

        private static void ReadLoop()
        {
            try
            {
                while (isRunning && reader != null)
                {
                    string packet = reader.ReadLine();
                    if (packet == null) break; // Client disconnected gracefully

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        PacketHandler.HandlePacket(packet);
                    });
                }
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    BetterMultiplayer.Instance.LogError("Read loop error: " + ex);
                }
            }
        }
    }
}
