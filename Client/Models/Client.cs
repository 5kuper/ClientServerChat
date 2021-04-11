﻿using System;
using System.Net.Sockets;
using Packets;

namespace ClientSide.Models
{
    public class Client
    {
        public string ID { get; set; }
        public string Username { get; set; }

        public bool Connected => _socket?.Connected ?? false;

        public PacketHandler Handler { get; }

        private TcpClient _socket;
        private NetworkStream _stream;

        private readonly byte[] _buffer = new byte[4096];

        public Action<string> Log { get; private set; } = s => { };
        public event Action<string> OnLog
        {
            add => Log += value;
            remove => Log -= value;
        }

        public Action<string, string> WriteMessage { get; private set; } = (s, c) => { };
        public event Action<string, string> OnWriteMessage
        {
            add => WriteMessage += value;
            remove => WriteMessage -= value;
        }

        public Action RaiseConnectionStatusChanged { get; private set; } = () => { };
        public event Action OnConnectionStatusChanged
        {
            add => RaiseConnectionStatusChanged += value;
            remove => RaiseConnectionStatusChanged -= value;
        }

        public Client()
        {
            Handler = new PacketHandler(this);
        }

        public void Connect(string host, int port)
        {
            _socket = new TcpClient();
            _socket.BeginConnect(host, port, ConnectCallback, _socket);
        }

        public void ConnectCallback(IAsyncResult result)
        {
            try
            {
                _socket.EndConnect(result);
            }
            catch (Exception e)
            {
                if (e is ObjectDisposedException)
                {
                    return; // Client disconnected
                }

                Log($"Failed to connect to server: {e.Message}");
                Disconnect();
            }

            if (!_socket.Connected)
            {
                return;
            }

            _stream = _socket.GetStream();
            SendPacket(new ConnectionRequestPacket(Username)); 
            _stream.BeginRead(_buffer, 0, _buffer.Length, ReceiveCallback, null);

            Log($"You have connected to the server as \"{Username}\".");
            RaiseConnectionStatusChanged();
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                int byteLength = _stream.EndRead(result);
                if (byteLength <= 0)
                {
                    return;
                }

                byte[] data = new byte[byteLength];
                Array.Copy(_buffer, data, byteLength);

                try
                {
                    Packet packet = Packet.Deserialize(data);
                    Handler.Handle(packet);
                }
                catch (Exception e)
                {
                    if (e is ObjectDisposedException)
                    {
                        return; // Client disconnected
                    }

                    Log("Received data that cannot be deserialized to a packet!");
                    Disconnect();
                }

                _stream.BeginRead(_buffer, 0, _buffer.Length, ReceiveCallback, null);
            }
            catch (Exception e)
            {
                if (e is ObjectDisposedException)
                {
                    return; // Client disconnected
                }

                Log($"Failed to receive data from server: {e.Message}");
                Disconnect();
            }
        }

        public void SendPacket(Packet packet)
        {
            try
            {
                packet.Serialize(out byte[] data);
                _stream?.WriteAsync(data, 0, data.Length);
            }
            catch (Exception e)
            {
                if (e is ObjectDisposedException)
                {
                    return; // Client disconnected
                }

                Log($"Failed to send data to server: {e.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            SendPacket(new NotificationPacket(NotificationCode.ClientDisconnecting));

            _stream?.Close();
            _socket?.Close();

            Log("You have disconnected from the server.");
            RaiseConnectionStatusChanged();
        }
    }
}