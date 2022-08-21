using MultiProcessManagedMachine;
using System;
using System.IO.Pipes;
using System.Windows;

namespace MultiProcessManagedMachineClient
{
    class ProcessClient
    {
        private string pipeID;
        private NamedPipeClientStream pipeClientStream;
        //List<float[]> listOfFloatArrays; // Test allocating lots of memory
        Random rand = new Random(1234);
        public ProcessClient(string paramID)
        {
            // Handle the ApplicationExit event to know when the application is exiting.
            Application.Current.Exit += Current_Exit;
            pipeID = paramID;

            StartIPC();

            //Random rand = new Random(45146);
            //listOfFloatArrays = new List<float[]>();
            //for (int i = 0; i < 2000; i++)
            //{
            //    float[] f = new float[1000 * 1000];

            //    for (int j = 0; j < 10000; j++)
            //    {
            //        int rndPos = (int)(f.Length * rand.NextDouble() * 0.9);
            //        f[rndPos] = (float)rand.NextDouble();
            //    }
            //    listOfFloatArrays.Add(f);
            //}

            StartCommsLoop();
        }

        private void StartCommsLoop()
        {
            while (true)
            {
                try
                {
                    byte[] commandBuf = new byte[Protocol.SIZE_OF_COMMAND];
                    pipeClientStream.Read(commandBuf, 0, Protocol.SIZE_OF_COMMAND);
                    Protocol.Command command = Protocol.ParseMessageCommand(commandBuf);

                    switch (command)
                    {
                        case Protocol.Command.Ping:
                            {
                                byte[] response = Protocol.CreateMessageCommand(Protocol.Command.Pong);
                                pipeClientStream.Write(response, 0, Protocol.SIZE_OF_COMMAND);
                                pipeClientStream.Flush();
                            }
                            break;
                        case Protocol.Command.Pong:
                            break;
                        case Protocol.Command.Exit:
                            Application.Current.Shutdown();
                            break;
                        case Protocol.Command.GetSamples:
                            {
                                // Read pos in sample
                                byte[] buf = new byte[sizeof(Int32)];
                                pipeClientStream.Read(buf, 0, sizeof(Int32));
                                int posInSample = BitConverter.ToInt32(buf, 0);
                                // Read number of samples requested
                                pipeClientStream.Read(buf, 0, sizeof(Int32));
                                int numSamples = BitConverter.ToInt32(buf, 0);

                                // Respond with noise

                                int[] samplesToSend = new int[numSamples];
                                for (int i = 0; i < numSamples; i++)
                                    samplesToSend[i] = (int)(rand.NextDouble() * 32768.0);
                                byte[] response = Protocol.CreateMessageSendSamples(samplesToSend, numSamples);
                                pipeClientStream.Write(response, 0, response.Length);
                                pipeClientStream.Flush();
                            }
                            break;
                    }
                }
                catch (Exception)
                {
                    Application.Current.Shutdown();
                    break;
                }
            }
        }

        private void Current_Exit(object sender, ExitEventArgs e)
        {
        }
        void StartIPC()
        {
            pipeClientStream = new NamedPipeClientStream(".",
                                                      pipeID,
                                                      PipeDirection.InOut,
                                                      PipeOptions.Asynchronous);

            try
            {
                pipeClientStream.Connect(3000);
            }
            catch
            {
                return;
            }
        }
    }
}
