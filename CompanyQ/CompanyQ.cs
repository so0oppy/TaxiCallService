using System;
using System.Text.Json;
using SolaceSystems.Solclient.Messaging;
using System.Threading;

namespace TaxiCallService
{
    class CompanyQ : IDisposable
    {
        string VPNName { get; set; }
        string UserName { get; set; }
        string Password { get; set; }

        const int DefaultReconnectRetries = 3;

        private ISession Session = null;
        private EventWaitHandle WaitEventWaitHandle = new AutoResetEvent(false);

        void Run(IContext context, string host)
        {
            if (context == null) throw new ArgumentException("Solace Systems API context Router must be not null.", "context");
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Solace Messaging Router host name must be non-empty.", "host");
            if (string.IsNullOrWhiteSpace(VPNName)) throw new InvalidOperationException("VPN name must be non-empty.");
            if (string.IsNullOrWhiteSpace(UserName)) throw new InvalidOperationException("Client username must be non-empty.");

            SessionProperties sessionProps = new SessionProperties()
            {
                Host = host,
                VPNName = VPNName,
                UserName = UserName,
                Password = Password,
                ReconnectRetries = DefaultReconnectRetries
            };

            Console.WriteLine($"Connecting as {UserName}@{VPNName} on {host}...");
            Session = context.CreateSession(sessionProps, HandleRequestMessage, null);
            ReturnCode returnCode = Session.Connect();
            if (returnCode == ReturnCode.SOLCLIENT_OK)
            {
                Console.WriteLine("Session successfully connected.");
                Session.Subscribe(ContextFactory.Instance.CreateTopic("PaymentRequest/>"), true);
                Console.WriteLine("Waiting for a payment request message...");
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
                Console.WriteLine("Received payment request message.");
                using (IMessage requestMessage = args.Message)
                {
                    var request = JsonSerializer.Deserialize<dynamic>(requestMessage.BinaryAttachment);
                    Console.WriteLine($"Payment request content: {request}");

                    using (IMessage replyMessage = ContextFactory.Instance.CreateMessage())
                    {
                        // PaymentRequest 메시지 생성 및 전송
                        replyMessage.Destination = ContextFactory.Instance.CreateTopic($"PaymentRequest/{request.UserID}");
                        replyMessage.BinaryAttachment = requestMessage.BinaryAttachment; // 받은 요청 그대로 전달
                        Console.WriteLine("Sending PaymentRequest to User...");
                        ReturnCode returnCode = Session.Send(replyMessage);
                        if (returnCode == ReturnCode.SOLCLIENT_OK)
                        {
                            Console.WriteLine("PaymentRequest sent to User.");
                        }
                        else
                        {
                            Console.WriteLine($"PaymentRequest failed, return code: {returnCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in HandleRequestMessage: {ex.Message}");
                // 예외 처리, 선택적으로 NACK 메시지 처리
            }
        }

        private void HandleSessionEvent(object sender, SessionEventArgs args)
        {
            Console.WriteLine($"Received session event: {args.Event}");
            switch (args.Event)
            {
                case SessionEvent.ReconnectFailed:
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
                Console.WriteLine("Usage: CompanyQ <host> <username>@<vpnname> <password>");
                Environment.Exit(1);
            }

            string[] split = args[1].Split('@');
            if (split.Length != 2)
            {
                Console.WriteLine("Usage: CompanyQ <host> <username>@<vpnname> <password>");
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
                    using (CompanyQ companyQ = new CompanyQ()
                    {
                        VPNName = vpnname,
                        UserName = username,
                        Password = password
                    })
                    {
                        companyQ.Run(context, host);
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
