using System;
using TaxiCallService;

namespace TaxiCallService
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Call Taxi?");
            string input = Console.ReadLine();
            if (input.ToLower() == "yes")
            {
                Console.WriteLine("Requesting Taxi...");

                // User sends RideRequest to TaxiPlatformOperator
                User.Main(new string[] { "tcp://localhost:55555", "user@vpn", "password" });

                // TaxiPlatformOperator processes the request and sends RideRequestResponse to User
                TaxiPlatformOperator.Main(new string[] { "tcp://localhost:55555", "operator@vpn", "password" });

                // Simulate that the taxi is found and User receives RideRequestResponse
                Console.WriteLine("Number 1234 Taxi will be arrived!");

                Console.WriteLine("Taxi is arrived! Would you like to board?");
                input = Console.ReadLine();
                if (input.ToLower() == "yes")
                {
                    // User boards the taxi
                    Console.WriteLine("Number 1234 User board the taxi.");

                    // Driver sends PickupComplete to TaxiPlatformOperator
                    Driver.Main(new string[] { "tcp://localhost:55555", "driver@vpn", "password" });

                    // Taxi arrives at destination
                    Console.WriteLine("Taxi arrived at destination!");

                    // Driver sends DropoffComplete to Payment
                    Driver.Main(new string[] { "tcp://localhost:55555", "driver@vpn", "password" });

                    // Payment sends PaymentRequest to CompanyQ
                    Payment.Main(new string[] { "tcp://localhost:55555", "payment@vpn", "password" });

                    // CompanyQ processes the payment request
                    CompanyQ.Main(new string[] { "tcp://localhost:55555", "companyq@vpn", "password" });

                    // Payment is processed
                    Console.WriteLine("The User should pay 50 dollars.");
                }
            }
        }
    }
}
