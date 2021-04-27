/*
 * Comms.cs - part of CNC Controls library
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
using System.Windows.Threading;

namespace CNC.Core
{
    public delegate void DataReceivedHandler(string data);

    public class Comms
    {
        public enum State
        {
            AwaitAck,
            DataReceived,
            ACK,
            NAK
        }

        public enum ResetMode
        {
            None,
            DTR,
            RTS
        }

        public enum StreamType
        {
            Serial,
            Telnet,
            Websocket
        }

        public const int TXBUFFERSIZE = 4096, RXBUFFERSIZE = 1024;

        public static StreamComms com = null;
    }

    public interface StreamComms
    {
        bool IsOpen { get; }
        int OutCount { get; }
        string Reply { get; }
        Comms.StreamType StreamType { get; }
        Comms.State CommandState { get; set; }
        bool EventMode { get; set; }
        Action<int> ByteReceived { get; set; }

        void Close();
        int ReadByte();
        void WriteByte(byte data);
        void WriteBytes(byte[] bytes, int len);
        void WriteString(string data);
        void WriteCommand(string command);
        string GetReply(string command);
        void AwaitAck();
        void AwaitAck(string command);
        void AwaitResponse(string command);
        void AwaitResponse();
        void PurgeQueue();

        event DataReceivedHandler DataReceived;
    }

    public static class EventUtils
    {
        public static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public static object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;

            return null;
        }
    }
}
