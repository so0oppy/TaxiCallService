using System;
using System.Text;
using System.Text.Json;
using SolaceSystems.Solclient.Messaging;
using System.Threading;

namespace TaxiCallService
{
    class Driver : IDisposable
    {
        string VPNName { get; set; }
        string UserName { get; set; }
        string Password { get; set; }
        string DriverID { get; set; } = "Driver123";

        const int DefaultReconnectRetries = 3;

        private ISession Session = null;
        private EventWaitHandle WaitEventWaitHandle = new AutoResetEvent(false);

        void Run(IContext context, string host)
        {
            if (context == null) throw new ArgumentException("Solace Systems API context Router must be not null.", "context");
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Solace Messaging Router host name must be non-empty.", "host");
            if (string.IsNullOrWhiteSpace(VPNName)) throw new InvalidOperationException("VPN name must be non-empty.");
            if (string.IsNullOrWhiteSpace(UserName)) throw new InvalidOperationException("Client username must be non-empty.");
            if (string.IsNullOrWhiteSpace(DriverID)) throw new InvalidOperationException("Driver ID must be non-empty.");

            SessionProperties sessionProps = new SessionProperties()
            {
                Host = host,
                VPNName = VPNName,
                UserName = UserName,
                Password = Password,
                ReconnectRetries = DefaultReconnectRetries
            };

            Console.WriteLine($"Connecting as {UserName}@{VPNName} on {host}...");
            Session = context.CreateSession(sessionProps, HandleRequestMessage, HandleSessionEvent);
            ReturnCode returnCode = Session.Connect();
            if (returnCode == ReturnCode.SOLCLIENT_OK)
            {
                Console.WriteLine("Session successfully connected.");

                // Subscribe to pickup request topic
                Session.Subscribe(ContextFactory.Instance.CreateTopic($"PickupRequest/{DriverID}/>"), true);
                Console.WriteLine("Waiting for a request to come in...");
                WaitEventWaitHandle.WaitOne();
            }
            else
            {
                Console.WriteLine($"Error connecting, return code: {returnCode}");
            }
        }

        private void HandleRequestMessage(object source, MessageEventArgs args)
        {
            try
            {
                Console.WriteLine("Received pickup request.");
                using (IMessage requestMessage = args.Message)
                {
                    Console.WriteLine($"Request content: {Encoding.UTF8.GetString(requestMessage.BinaryAttachment)}");

                    using (IMessage replyMessage = ContextFactory.Instance.CreateMessage())
                    {
                        string rideID = Guid.NewGuid().ToString();
                        // Create response message using JSON serialization
                        var response = new { Timestamp = DateTime.UtcNow, RideID = rideID, Location = "LocationA" };
                        replyMessage.BinaryAttachment = JsonSerializer.SerializeToUtf8Bytes(response);
                        Console.WriteLine("Sending reply...");
                        ReturnCode returnCode = Session.SendReply(requestMessage, replyMessage);
                        if (returnCode == ReturnCode.SOLCLIENT_OK)
                        {
                            Console.WriteLine("Sent reply.");
                            SendRideRequestResponse();
                            SendPickupComplete(rideID, "LocationA");
                            SendDropoffComplete(rideID, "LocationB", "UserID"); // Replace with actual UserID
                            SendLocationUpdate("LocationA", "AVAILABLE");
                        }
                        else
                        {
                            Console.WriteLine($"Reply failed, return code: {returnCode}");
                        }
                        WaitEventWaitHandle.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in HandleRequestMessage: {ex.Message}");
                // Handle exception, optionally NACK the message
            }
        }

        private void SendRideRequestResponse()
        {
            using (IMessage message = ContextFactory.Instance.CreateMessage())
            {
                // Create RideRequestResponse message using JSON serialization
                var response = new { Timestamp = DateTime.UtcNow, Result = "SUCCESS", RideID = Guid.NewGuid(), ETA = "5 minutes", TaxiNumber = "1234" };
                message.Destination = ContextFactory.Instance.CreateTopic("RideRequestResponse/User123");
                message.BinaryAttachment = JsonSerializer.SerializeToUtf8Bytes(response);

                Console.WriteLine("Sending RideRequestResponse...");
                ReturnCode returnCode = Session.Send(message);
                if (returnCode == ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine("RideRequestResponse sent.");
                }
                else
                {
                    Console.WriteLine($"RideRequestResponse failed, return code: {returnCode}");
                }
            }
        }

        private void SendPickupComplete(string rideID, string location)
        {
            using (IMessage message = ContextFactory.Instance.CreateMessage())
            {
                // Create PickupComplete message using JSON serialization
                var response = new { Timestamp = DateTime.UtcNow, RideID = rideID, Location = location };
                message.Destination = ContextFactory.Instance.CreateTopic("PickupRequestResponse");
                message.BinaryAttachment = JsonSerializer.SerializeToUtf8Bytes(response);

                Console.WriteLine("Sending PickupComplete...");
                ReturnCode returnCode = Session.Send(message);
                if (returnCode == ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine("PickupComplete sent.");
                }
                else
                {
                    Console.WriteLine($"PickupComplete failed, return code: {returnCode}");
                }
            }
        }

        private void SendDropoffComplete(string rideID, string location, string userID)
        {
            using (IMessage message = ContextFactory.Instance.CreateMessage())
            {
                // Create DropoffComplete message using JSON serialization
                var response = new { Timestamp = DateTime.UtcNow, RideID = rideID, Location = location, UserID = userID };
                message.Destination = ContextFactory.Instance.CreateTopic("DropoffComplete");
                message.BinaryAttachment = JsonSerializer.SerializeToUtf8Bytes(response);

                Console.WriteLine("Sending DropoffComplete...");
                ReturnCode returnCode = Session.Send(message);
                if (returnCode == ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine("DropoffComplete sent.");
                }
                else
                {
                    Console.WriteLine($"DropoffComplete failed, return code: {returnCode}");
                }
            }
        }

        private void SendLocationUpdate(string location, string status)
        {
            using (IMessage message = ContextFactory.Instance.CreateMessage())
            {
                // Create LocationUpdate message using JSON serialization
                var response = new { Timestamp = DateTime.UtcNow, DriverID = DriverID, Location = location, Status = status };
                message.Destination = ContextFactory.Instance.CreateTopic($"LocationUpdate/{DriverID}/{status}/{location}");
                message.BinaryAttachment = JsonSerializer.SerializeToUtf8Bytes(response);

                Console.WriteLine("Sending LocationUpdate...");
                ReturnCode returnCode = Session.Send(message);
                if (returnCode == ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine("LocationUpdate sent.");
                }
                else
                {
                    Console.WriteLine($"LocationUpdate failed, return code: {returnCode}");
                }
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
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: Driver <host> <username>@<vpnname> <password> <driverID>");
                Environment.Exit(1);
            }

            string[] split = args[1].Split('@');
            if (split.Length != 2)
            {
                Console.WriteLine("Usage: Driver <host> <username>@<vpnname> <password> <driverID>");
                Environment.Exit(1);
            }

            string host = args[0];
            string username = split[0];
            string vpnname = split[1];
            string password = args[2];
            string driverID = args[3];

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
                    using (Driver driver = new Driver()
                    {
                        VPNName = vpnname,
                        UserName = username,
                        Password = password,
                        DriverID = driverID
                    })
                    {
                        driver.Run(context, host);
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
