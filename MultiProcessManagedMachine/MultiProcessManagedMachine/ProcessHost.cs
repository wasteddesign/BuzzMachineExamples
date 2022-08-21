using Buzz.MachineInterface;
using BuzzGUI.Common;
using System;
using System.Diagnostics;
using System.IO.Pipes;

namespace MultiProcessManagedMachine
{
    class ProcessHost : IDisposable
    {
        public const int BUFFER_SIZE = 1024 * 2;

        private string PipeID;
        private Process childProcess;
        private NamedPipeServerStream pipeServerStream;

        public bool MachineClosing { get; private set; }

        public ProcessHost()
        {
            MachineClosing = false;
        }

        public bool Start(string paramUID)
        {
            PipeID = paramUID;

            ProcessStartInfo processInfo = new ProcessStartInfo(Global.BuzzPath + "\\Gear\\Effects\\MultiProcessManagedMachineClient", this.PipeID);
            childProcess = Process.Start(processInfo);

            // We do everything synchronously
            StartIPCServer();

            return true;
        }

        public bool Send(Protocol.Command command)
        {
            byte[] response = Protocol.CreateMessageCommand(command);
            pipeServerStream.Write(response, 0, Protocol.SIZE_OF_COMMAND);
            pipeServerStream.Flush();

            return true;
        }

        internal bool Read(Protocol.Command command)
        {
            bool ret = false;
            byte[] response = new byte[Protocol.SIZE_OF_COMMAND];
            pipeServerStream.Read(response, 0, Protocol.SIZE_OF_COMMAND);
            if ((Protocol.Command)BitConverter.ToInt32(response, 0) == command)
                ret = true;

            return ret;
        }

        void StartIPCServer()
        {
            if (pipeServerStream == null)
            {
                pipeServerStream = new NamedPipeServerStream(PipeID,
                                                              PipeDirection.InOut,
                                                              1,
                                                              PipeTransmissionMode.Byte,
                                                              PipeOptions.Asynchronous,
                                                              BUFFER_SIZE,
                                                              BUFFER_SIZE);

            }

            try
            {
                pipeServerStream.WaitForConnection();
            }
            catch (Exception)
            {

            }
        }

        internal void RequestSamples(Sample[] samples)
        {
            try
            {
                byte[] requestMessage = Protocol.CreateMessageGetSamples(0, samples.Length * 2);
                pipeServerStream.Write(requestMessage, 0, requestMessage.Length);
                pipeServerStream.Flush();

                int readSize = 4 + 4 + 4 * samples.Length * 2; // We are expecting command + lenght + requested amount of floats
                byte[] responseMessage = new byte[readSize];

                pipeServerStream.Read(responseMessage, 0, readSize);
                float[] sampleData;
                int numRead;
                Protocol.ParseMessageGetSamples(responseMessage, out sampleData, out numRead);

                for (int i = 0; i < numRead / 2; i++)
                {
                    samples[i].L = sampleData[2 * i];
                    samples[i].R = sampleData[2 * i + 1];
                }
            }
            catch
            {
                // Pipe broken. Restart process or something.
                TryToRecover();
            }
        }

        void TryToRecover()
        {
            if (!MachineClosing)
            {
                DisposeClientProcess();
                Start(PipeID);
            }
        }

        void DisposeClientProcess()
        {
            MachineClosing = true;
            try
            {
                childProcess.Kill();
            }
            catch
            {
            }

            pipeServerStream.Dispose();
            pipeServerStream = null;
        }

        #region IDisposable Members

        public void Dispose()
        {
            DisposeClientProcess();
        }

        #endregion
    }
}
