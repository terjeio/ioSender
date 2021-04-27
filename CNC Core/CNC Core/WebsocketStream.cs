/*
 * WebsocketStream.cs - part of CNC Controls library
 *
 * v0.31 / 2021-04-23 / Io Engineering (Terje Io)
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
using System.Windows.Threading;
using WebSocketSharp;

namespace CNC.Core
{
#if USEWEBSOCKET
    public class WebsocketStream : StreamComms
    {
        private WebSocket websocket = null;
        private volatile bool _isOpen = false;
        private volatile Comms.State state = Comms.State.ACK;
        private StringBuilder input = new StringBuilder(1024);
        private Dispatcher Dispatcher { get; set; }

        public event DataReceivedHandler DataReceived;

        public WebsocketStream(string host, Dispatcher dispatcher)
        {
            Comms.com = this;
            Reply = string.Empty;
            Dispatcher = dispatcher;

            try
            {
                websocket = new WebSocketSharp.WebSocket(host);
                websocket.OnMessage += OnMessage;
                websocket.OnOpen += OnOpen;
                websocket.OnClose += OnClose;
                websocket.Connect();
            }
            catch
            {
            }
        }

        ~WebsocketStream()
        {
            Close();
        }

        public Comms.StreamType StreamType { get { return Comms.StreamType.Websocket; } }
        public bool IsOpen { get { return websocket != null && _isOpen; } }
        public int OutCount { get { return 0; } }
        public Comms.State CommandState { get { return state; } set { state = value; } }
        public string Reply { get; private set; }
        public bool EventMode { get; set; } = true;
        public Action<int> ByteReceived { get; set; }

        public void PurgeQueue()
        {
            Reply = string.Empty;
            if (!EventMode)
                input.Clear();
        }

        public void Close()
        {
            if (IsOpen)
            {
                websocket.OnMessage -= OnMessage;
                websocket.OnOpen -= OnOpen;
                websocket.Close();
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
            websocket.Send(new byte[1] { data });
        }

        public void WriteBytes(byte[] bytes, int len)
        {
            websocket.Send(bytes);
        }

        public void WriteString(string data)
        {
            byte[] bytes = Encoding.Default.GetBytes(data);

            websocket.Send(bytes);
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

        private void OnOpen(object sender, EventArgs e)
        {
            _isOpen = true;
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            _isOpen = false;
            websocket.OnClose -= OnClose;
            websocket = null;
        }
        private int gp()
        {
            int pos = 0; bool found = false;

            while (!found && pos < input.Length)
                found = input[pos++] == '\n';

            return found ? pos - 1 : 0;
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            int pos = 0;

            lock (input)
            {
                if (e.IsText)
                    input.Append(e.Data);
                else
                    input.Append(Encoding.Default.GetString(e.RawData, 0, e.RawData.Length));

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
            }
        }
    }
#endif
}
