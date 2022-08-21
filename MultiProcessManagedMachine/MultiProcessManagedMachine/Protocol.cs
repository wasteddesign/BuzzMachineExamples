using System;
using System.IO;

namespace MultiProcessManagedMachine
{
    public class Protocol
    {
        public static readonly int SIZE_OF_COMMAND = sizeof(Int32);
        public enum Command
        {
            Ping,
            Pong,
            Exit,
            GetSamples,
            SendSamples
        }

        public static Command ParseMessageCommand(byte[] commandBuf)
        {
            Command ret = (Command)BitConverter.ToInt32(commandBuf, 0);
            return ret;
        }

        public static byte[] CreateMessageCommand(Command command)
        {
            byte[] ret = BitConverter.GetBytes((Int32)command);
            return ret;
        }

        public static byte[] CreateMessageGetSamples(int positionInSample, int numSamples)
        {
            byte[] ret = null;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes((Int32)Protocol.Command.GetSamples), 0, sizeof(Int32));
                ms.Write(BitConverter.GetBytes((Int32)positionInSample), 0, sizeof(Int32));
                ms.Write(BitConverter.GetBytes((Int32)numSamples), 0, sizeof(Int32));

                ret = ms.ToArray();
            }

            return ret;
        }

        public static void ParseMessageGetSamples(byte[] commandBuf, out float[] samples, out int numRead)
        {
            int command = BitConverter.ToInt32(commandBuf, 0);
            numRead = BitConverter.ToInt32(commandBuf, 4);
            samples = new float[numRead];
            int pos = 8;

            float convertToFloat = 1.0f / 32768.0f;

            for (int i = 0; i < numRead; i++)
            {
                samples[i] = (float)BitConverter.ToInt32(commandBuf, pos) * convertToFloat;
                pos += 4;
            }
        }

        public static byte[] CreateMessageSendSamples(int[] sampleBuf, int numSamples)
        {
            byte[] ret = null;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes((Int32)Protocol.Command.SendSamples), 0, sizeof(Int32));
                ms.Write(BitConverter.GetBytes((Int32)numSamples), 0, sizeof(Int32));

                for (int i = 0; i < numSamples; i++)
                {
                    ms.Write(BitConverter.GetBytes((Int32)sampleBuf[i]), 0, sizeof(Int32));
                }

                ret = ms.ToArray();
            }

            return ret;
        }
    }
}
