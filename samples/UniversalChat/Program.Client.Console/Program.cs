using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TypeAlias;
using UniversalChat.Interface;

namespace UniversalChat.Program.Client.Console
{
    class ChatConsole : IUserEventObserver
    {
        private Communicator _communicator;
        private SlimTaskRequestWaiter _requestWaiter;
        private UserRef _user;
        private Dictionary<string, OccupantRef> _occupantMap = new Dictionary<string, OccupantRef>();
        private string _currentRoomName;

        public async Task RunAsync(Communicator communicator)
        {
            _communicator = communicator;
            _requestWaiter = new SlimTaskRequestWaiter(communicator);

            _user = await LoginAsync();
            await EnterRoomAsync("#general");
            await ChatLoopAsync();
        }

        private async Task<UserRef> LoginAsync()
        {
            var userLogin = new UserLoginRef(new SlimActorRef(1), _requestWaiter, null);

            var observerId = _communicator.IssueObserverId();
            _communicator.AddObserver(observerId, new ObserverEventDispatcher(this));

            try
            {
                var userActorId = await userLogin.Login("console", "1234", observerId);
                return new UserRef(new SlimActorRef(userActorId), _requestWaiter, null);
            }
            catch (Exception)
            {
                _communicator.RemoveObserver(observerId);
                throw;
            }
        }

        private async Task ChatLoopAsync()
        {
            while (true)
            {
                var line = await ReadLineAsync();
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("/"))
                {
                    var words = trimmedLine.Split();
                    switch (words[0].ToLower())
                    {
                        case "/e":
                        case "/enter":
                        case "/j":
                        case "/join":
                            try
                            {
                                await EnterRoomAsync(words[1]);
                            }
                            catch (Exception e)
                            {
                                System.Console.WriteLine("Failed to join.");
                                System.Console.WriteLine(e);
                            }
                            break;

                        case "/x":
                        case "/exit":
                        case "/l":
                        case "/leave":
                            try
                            {
                                var leaveRoom = words.Length > 1 ? words[1] : _currentRoomName;
                                await ExitRoomAsync(leaveRoom);
                                _occupantMap.Remove(leaveRoom);
                                if (_occupantMap.ContainsKey(_currentRoomName) == false)
                                    _currentRoomName = _occupantMap.Keys.FirstOrDefault();
                            }
                            catch (Exception e)
                            {
                                System.Console.WriteLine("Failed to join.");
                                System.Console.WriteLine(e);
                            }
                            break;

                        case "/c":
                        case "/current":
                            if (words.Length > 1)
                            {
                                var roomName = words[1];
                                if (_occupantMap.ContainsKey(roomName))
                                {
                                    _currentRoomName = roomName;
                                    System.Console.WriteLine($"Current room is changed to {_currentRoomName}");
                                }
                                else
                                {
                                    System.Console.WriteLine($"No room");
                                }
                            }
                            else
                            {
                                System.Console.WriteLine($"Current room: {_currentRoomName}");
                            }
                            break;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(_currentRoomName))
                    {
                        System.Console.WriteLine("Need a room to say");
                    }
                    else
                    {
                        await _occupantMap[_currentRoomName].Say(line);
                    }
                }
            }
        }

        private Task<string> ReadLineAsync()
        {
            // When use plain "System.Console.ReadLine" in async loop,
            // it stops TcpConnection from receiving data so that
            // we cannot read any chat message while reading console.
            // To avoid this problem we read console in another thread in ThreadPool.
            var tcs = new TaskCompletionSource<string>();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                tcs.SetResult(System.Console.ReadLine());
            });
            return tcs.Task;
        }

        private async Task<RoomInfo> EnterRoomAsync(string name)
        {
            var observerId = _communicator.IssueObserverId();
            _communicator.AddObserver(observerId, new ObserverEventDispatcher(new RoomConsole(name)));
            try
            {
                var ret = await _user.EnterRoom(name, observerId);
                var occupant = new OccupantRef(new SlimActorRef(ret.Item1), _requestWaiter, null);
                _occupantMap.Add(name, occupant);
                _currentRoomName = name;
                return ret.Item2;
            }
            catch (Exception)
            {
                _communicator.RemoveObserver(observerId);
                throw;
            }
        }

        private async Task ExitRoomAsync(string name)
        {
            await _user.ExitFromRoom(name);
            // TODO: Remove Observer
        }

        void IUserEventObserver.Invite(string invitorUserId, string roomName)
        {
            System.Console.WriteLine($"<Invite> [invitorUserId] invites you to {roomName}");
        }

        void IUserEventObserver.Whisper(ChatItem chatItem)
        {
            System.Console.WriteLine($"<Whisper> {chatItem.UserId}: {chatItem.Message}");
        }

        private class RoomConsole : IRoomObserver
        {
            private string _name;

            public RoomConsole(string name)
            {
                _name = name;
            }

            public void Enter(string userId)
            {
                System.Console.WriteLine($"[{_name}] {userId} Entered");
            }

            public void Exit(string userId)
            {
                System.Console.WriteLine($"[{_name}] {userId} Exited");
            }

            public void Say(ChatItem chatItem)
            {
                System.Console.WriteLine($"[{_name}] {chatItem.UserId}: {chatItem.Message}");
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var serializer = new PacketSerializer(
                new PacketSerializerBase.Data(
                    new ProtoBufMessageSerializer(TypeModel.Create()),
                    new TypeAliasTable()));

            var communicator = new Communicator(LogManager.GetLogger("Communicator"),
                                                new IPEndPoint(IPAddress.Loopback, 9001),
                                                _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
            communicator.Start();

            var driver = new ChatConsole();
            driver.RunAsync(communicator).Wait();
        }
    }
}
