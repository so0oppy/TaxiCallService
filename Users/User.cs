using System;
using System.Text;
using System.Text.Json;
using SolaceSystems.Solclient.Messaging;
using System.Threading;

namespace TaxiCallService
{
    class User : IDisposable
    {
        string VPNName { get; set; }
        string UserName { get; set; }
        string Password { get; set; }
        string UserID { get; set; } = "User123";

        const int DefaultReconnectRetries = 3;

        private ISession Session = null;
        private EventWaitHandle WaitEventWaitHandle = new AutoResetEvent(false);

        void Run(IContext context, string host)
        {
            if (context == null) throw new ArgumentException("Solace Systems API context Router must be not null.", "context");
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Solace Messaging Router host name must be non-empty.", "host");
            if (string.IsNullOrWhiteSpace(VPNName)) throw new InvalidOperationException("VPN name must be non-empty.");
            if (string.IsNullOrWhiteSpace(UserName)) throw new InvalidOperationException("Client username must be non-empty.");
            if (string.IsNullOrWhiteSpace(UserID)) throw new InvalidOperationException("User ID must be non-empty.");

            SessionProperties sessionProps = new SessionProperties()
            {
                Host = host,
                VPNName = VPNName,
                UserName = UserName,
                Password = Password,
                ReconnectRetries = DefaultReconnectRetries
            };

            Console.WriteLine($"Connecting as {UserName}@{VPNName} on {host}...");
            Session = context.CreateSession(sessionProps, HandleMessage, HandleSessionEvent);
            ReturnCode returnCode = Session.Connect();
            if (returnCode == ReturnCode.SOLCLIENT_OK)
            {
                Console.WriteLine("Session successfully connected.");
                Session.Subscribe(ContextFactory.Instance.CreateTopic($"PaymentRequest/{UserID}/>"), true);
                Console.WriteLine("Waiting for a response...");
                SendRideRequest();
                WaitEventWaitHandle.WaitOne();
            }
            else
            {
                Console.WriteLine($"Error connecting, return code: {returnCode}");
            }
        }

        private void SendRideRequest()
        {
            using (IMessage message = ContextFactory.Instance.CreateMessage())
            {
                var rideRequest = new { Timestamp = DateTime.UtcNow, UserID = UserID, CurrentLocation = "LocationA", Destination = "LocationB" };
                message.Destination = ContextFactory.Instance.CreateTopic("taxi/requests");
                message.BinaryAttachment = JsonSerializer.SerializeToUtf8Bytes(rideRequest);

                Console.WriteLine("Sending ride request...");
                ReturnCode returnCode = Session.Send(message);
                if (returnCode == ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine("Ride request sent.");
                }
                else
                {
                    Console.WriteLine($"Ride request failed, return code: {returnCode}");
                }
            }
        }

        private void HandleMessage(object source, MessageEventArgs args)
        {
            try
            {
                Console.WriteLine("Received response.");
                using (IMessage message = args.Message)
                {
                    Console.WriteLine($"Response content: {Encoding.UTF8.GetString(message.BinaryAttachment)}");
                    WaitEventWaitHandle.Set();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in HandleMessage: {ex.Message}");
                // 예외 처리, 선택적으로 NACK 메시지 처리
            }
        }

        private void HandleSessionEvent(object sender, SessionEventArgs args)
        {
            Console.WriteLine($"Received session event: {args.Event}");
            switch (args.Event)
            {
                case SessionEvent.Reconnecting:
                case SessionEvent.Disconnected:
                    Console.WriteLine("Session connection failed or disconnected. Exiting...");
                    WaitEventWaitHandle.Set();
                    break;
                default:
                    break;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Session != null)
                {
                    Session.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: User <host> <username>@<vpnname> <password>");
                Environment.Exit(1);
            }

            string[] split = args[1].Split('@');
            if (split.Length != 2)
            {
                Console.WriteLine("Usage: User <host> <username>@<vpnname> <password>");
                Environment.Exit(1);
            }

            string host = args[0];
            string username = split[0];
            string vpnname = split[1];
            string password = args[2];

            ContextFactoryProperties cfp = new ContextFactoryProperties()
            {
                SolClientLogLevel = SolLogLevel.Warning
            };
            cfp.LogToConsoleError();
            ContextFactory.Instance.Init(cfp);

            try
            {
                using (IContext context = ContextFactory.Instance.CreateContext(new ContextProperties(), null))
                {
                    using (User user = new User()
                    {
                        VPNName = vpnname,
                        UserName = username,
                        Password = password,
                        UserID = username
                    })
                    {
                        user.Run(context, host);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception thrown: {ex.Message}");
            }
            finally
            {
                ContextFactory.Instance.Cleanup();
            }
            Console.WriteLine("Finished");
        }
    }
}
