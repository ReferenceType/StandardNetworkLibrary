using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpMessageBenchmark
{
    internal class ConsoleInputHandler
    {
        public readonly ref struct ConfigInputs
        {
            public readonly bool runAsServer;
            public readonly bool isFixedMessage;
            public readonly int fixedMessageSize;
            public readonly bool runAsClient;
            public readonly int numClients;
            public readonly int numMessages;
            public readonly int messageSize;

            public ConfigInputs(bool runAsServer, bool runAsClient, bool isFixedMessage, int fixedMessageSize, int numClients, int numMessages, int messageSize)
            {
                this.runAsServer = runAsServer;
                this.runAsClient = runAsClient;
                this.isFixedMessage = isFixedMessage;
                this.fixedMessageSize = fixedMessageSize;
                this.numClients = numClients;
                this.numMessages = numMessages;
                this.messageSize = messageSize;
            }
        }
        enum InputState
        {
            ObtainModeServer,
            ServerStaticResponse,
            ServerStaticResponseSize,
            ObtainModeClient,
            NumClients,
            NumMessages,
            MessageSize,
            Done
        }
        static bool runAsServer;
        static bool isFixedMessage;
        static int fixedMessageSize;

        static bool runAsClient;
        static int numClients;
        static int numMessages;
        static int messageSize;
        private static InputState inputState = InputState.ObtainModeServer;

        public static ConfigInputs ObtainConfig()
        {
            Console.WriteLine("[Hint] You can enter a command for quick configure");
            Console.WriteLine("'xx' Server Client on same application");
            Console.WriteLine("'s0' is Server with fixed response");
            Console.WriteLine("'s1' is Server with dynamic echo response");
            Console.WriteLine("'c0' is 100 Client, 1000 Message, 32 byte payload");
            Console.WriteLine();
            string input = "";
            bool isYes() => input.Equals("y", StringComparison.OrdinalIgnoreCase);
            bool isNo() => input.Equals("n", StringComparison.OrdinalIgnoreCase);

            while (true)
            {
                switch (inputState)
                {
                    case InputState.ObtainModeServer:

                        Console.WriteLine("-> Run As Echo Server? <y/n>?");
                        input = Console.ReadLine() ?? "";
                        if (CheckQuickConfig(input))
                            continue;

                        if (!isYes() && !isNo())
                            continue;
                        if (isYes())
                        {
                            runAsServer = true;
                            inputState = InputState.ServerStaticResponse;
                        }
                        else
                            inputState = InputState.ObtainModeClient;

                        continue;

                    case InputState.ServerStaticResponse:
                        Console.WriteLine("-> Respond Fixed Static Message? <y/n>?");
                        input = Console.ReadLine() ?? "";
                        if (!isYes() && !isNo())
                            continue;
                        if (isYes())
                        {
                            isFixedMessage = isYes();
                            inputState = InputState.ServerStaticResponseSize;
                        }
                        else
                        {
                            inputState = InputState.ObtainModeClient;
                            Console.WriteLine("Server will reply dynamic echo messages same size as incoming");
                        }
                        continue;

                    case InputState.ServerStaticResponseSize:
                        Console.WriteLine("-> Enter Fixed Response Message Size In Bytes:");
                        input = Console.ReadLine() ?? "";
                        if (int.TryParse(input, out var byteSize))
                        {
                            fixedMessageSize = byteSize;
                            inputState = InputState.ObtainModeClient;
                        }
                        else
                            Console.WriteLine("Enter a valid number");
                        continue;

                    case InputState.ObtainModeClient:

                        Console.WriteLine("-> Run As Client <y/n>?");
                        input = Console.ReadLine() ?? "";

                        if (!isYes() && !isNo())
                            continue;

                        runAsClient = isYes();
                        if (!runAsClient && !runAsServer)
                            inputState = InputState.ObtainModeServer;
                        else if (runAsClient)
                            inputState = InputState.NumClients;
                        else
                            inputState = InputState.Done; ;

                        continue;

                    case InputState.NumClients:
                        Console.WriteLine("-> Enter Number of Clients:");
                        input = Console.ReadLine() ?? "";
                        if (ushort.TryParse(input, out var num))
                        {
                            numClients = num;
                            inputState = InputState.NumMessages;
                        }
                        else
                            Console.WriteLine("Enter a valid number");
                        continue;


                    case InputState.NumMessages:
                        Console.WriteLine("-> Enter Number of Messages:");
                        input = Console.ReadLine() ?? "";
                        if (int.TryParse(input, out var nummsg))
                        {
                            numMessages = nummsg;
                            inputState = InputState.MessageSize;
                        }
                        else
                            Console.WriteLine("Enter a valid number");
                        continue;

                    case InputState.MessageSize:
                        Console.WriteLine("-> Enter Message Size In Bytes:");
                        input = Console.ReadLine() ?? "";
                        if (int.TryParse(input, out var byteSizefm))
                        {
                            messageSize = byteSizefm;
                            inputState = InputState.Done;
                        }
                        else
                            Console.WriteLine("Enter a valid number");
                        continue;


                    case InputState.Done:
                        Console.WriteLine("Configuration Complete\n");
                        string validate = "";
                        if (runAsServer)
                        {
                            validate += "Running as Server" + "\n";
                            validate += isFixedMessage ? "Server will Reply Fixed Message with size:" + fixedMessageSize :
                               "Server will Reply Dynamic Echo Messages" + "\n";
                        }
                        if (runAsClient)
                        {
                            validate += "\nRunning as Client" + "\n";
                            validate += "Number of Echo Clients: " + numClients.ToString() + "\n";
                            validate += "Number of Messages: " + numMessages.ToString() + "\n";
                            validate += "Message size in bytes: " + messageSize.ToString();

                        }
                        Console.WriteLine(validate);
                        Console.WriteLine("\n-> Confirm <y/n>?");
                        input = Console.ReadLine() ?? "";

                        if (!isYes() && !isNo())
                            continue;
                        if (isYes())
                            break;
                        else
                            Reset();
                        continue;
                }
                break;

            }
            return new ConfigInputs(runAsServer: runAsServer,
                                    runAsClient: runAsClient,
                                    isFixedMessage: isFixedMessage,
                                    fixedMessageSize: fixedMessageSize,
                                    numClients: numClients,
                                    numMessages: numMessages,
                                    messageSize: messageSize);

            void Reset()
            {
                runAsServer = false;
                runAsClient = false;
                isFixedMessage = false;
                fixedMessageSize = 0;
                numClients = 0;
                numMessages = 0;
                messageSize = 0;
                inputState = InputState.ObtainModeServer;
            }

        }

        private static bool CheckQuickConfig(string input)
        {
            if (input == "xx")
            {
                runAsServer = true;
                runAsClient = true;
                isFixedMessage = true;
                fixedMessageSize = 32;
                numClients = 100;
                numMessages = 1000;
                messageSize = 32;
                inputState = InputState.Done;
                return true;
            }
            if (input == "s0")
            {
                runAsServer = true;
                runAsClient = false;
                isFixedMessage = true;
                fixedMessageSize = 32;
                inputState = InputState.Done;
                return true;
            }
            if (input == "s1")
            {
                runAsServer = true;
                runAsClient = false;
                isFixedMessage = false;
                inputState = InputState.Done;
                return true;
            }
            if (input == "c0")
            {
                runAsServer = false;
                runAsClient = true;
                isFixedMessage = false;
                numClients = 100;
                numMessages = 1000;
                messageSize = 32;
                inputState = InputState.Done;
                return true;
            }
            return false;
        }


    }
}
