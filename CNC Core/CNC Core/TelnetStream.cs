/*
 * TelnetStream.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-30 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2021, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using System.Text;
using System.Net.Sockets;
using System.Windows.Threading;

namespace CNC.Core
{
    public class TelnetStream : StreamComms
    {
        private TcpClient ipserver = null;
        private NetworkStream ipstream = null;
        private byte[] buffer = new byte[512];
        private volatile Comms.State state = Comms.State.ACK;
        private StringBuilder input = new StringBuilder(1024);
        private Dispatcher Dispatcher { get; set; }

        public event DataReceivedHandler DataReceived;

        public TelnetStream(string host, Dispatcher dispatcher)
        {
            Comms.com = this;
            Reply = string.Empty;
            Dispatcher = dispatcher;

            if (!host.Contains(":"))
                host += ":23";

            string[] parameter = host.Split(':');

            if (parameter.Length == 2) try
            {
                ipserver = new TcpClient(parameter[0], int.Parse(parameter[1]));
                ipserver.NoDelay = true;
                ipstream = ipserver.GetStream();
                ipstream.BeginRead(buffer, 0, buffer.Length, ReadComplete, buffer);
            }
            catch
            {
            }
        }

        ~TelnetStream()
        {
            Close();
        }

        public Comms.StreamType StreamType { get { return Comms.StreamType.Telnet; } }
        public bool IsOpen { get { return ipserver != null && ipserver.Connected; } }
        public int OutCount { get { return 0; } }
        public Comms.State CommandState { get { return state; } set { state = value; } }
        public string Reply { get; private set; }
        public bool EventMode { get; set; } = true;
        public Action<int> ByteReceived { get; set; }

        public void PurgeQueue()
        {
            while (ipstream.DataAvailable)
                ipstream.ReadByte();
            Reply = string.Empty;
            if (!EventMode)
                input.Clear();
        }

        public void Close()
        {
            if (IsOpen)
            {
                PurgeQueue();
                ipstream.Close(300);
                ipstream.Dispose();
                ipstream = null;
                ipserver.Close();
                ipserver = null;
            }
        }

        public int ReadByte()
        {
            int c = input.Length == 0 ? -1 : input[0];

            if (c != -1)
                input.Remove(0, 1);

            return c;
        }

        public void WriteByte(byte data)
        {
            ipstream.WriteAsync(new byte[1] { data }, 0, 1);
        }

        public void WriteBytes(byte[] bytes, int len)
        {
            ipstream.WriteAsync(bytes, 0, len);
        }

        public void WriteString(string data)
        {
            byte[] bytes = Encoding.Default.GetBytes(data);
            ipstream.WriteAsync(bytes, 0, bytes.Length);
        }

        public void WriteCommand(string command)
        {
            state = Comms.State.AwaitAck;

            if (command.Length > 1 || command == GrblConstants.CMD_PROGRAM_DEMARCATION)
                command += "\r";

            WriteString(command);
        }

        public void AwaitAck()
        {
            while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
                EventUtils.DoEvents();
        }

        public void AwaitAck(string command)
        {
            WriteCommand(command);

            while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck) ;
        }

        public void AwaitResponse()
        {
            while (Comms.com.CommandState == Comms.State.AwaitAck)
                EventUtils.DoEvents();
        }
        public void AwaitResponse(string command)
        {
            WriteCommand(command);

            while (Comms.com.CommandState == Comms.State.AwaitAck) ;
        }

        public string GetReply(string command)
        {
            Reply = string.Empty;
            WriteCommand(command);

            while (state == Comms.State.AwaitAck)
                EventUtils.DoEvents();

            return Reply;
        }

        private int gp()
        {
            int pos = 0; bool found = false;

            while (!found && pos < input.Length)
                found = input[pos++] == '\n';

            return found ? pos - 1 : 0;
        }

        void ReadComplete(IAsyncResult iar)
        {
            int bytesAvailable = 0;
            byte[] buffer = (byte[])iar.AsyncState;

            try
            {
                bytesAvailable = ipstream.EndRead(iar);
            }
            catch
            {
                // error handling required here (and many other places)...
            }

            int pos = 0;

            lock (input)
            {
                input.Append(Encoding.ASCII.GetString(buffer, 0, bytesAvailable));

                if (EventMode)
                {
                    while (input.Length > 0 && (pos = gp()) > 0)
                    {
                        Reply = input.ToString(0, pos - 1);
                        input.Remove(0, pos + 1);
                        state = Reply == "ok" ? Comms.State.ACK : (Reply.StartsWith("error") ? Comms.State.NAK : Comms.State.DataReceived);
                        if (Reply.Length != 0 && DataReceived != null)
                            Dispatcher.Invoke(DataReceived, Reply);
                    }
                }
                else
                    ByteReceived?.Invoke(ReadByte());

                if (ipstream != null && ipserver.Connected)
                    ipstream.BeginRead(buffer, 0, buffer.Length, ReadComplete, buffer);
            }
        }
    }
}
