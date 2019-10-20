/*
 * ProfileData.cs - part of CNC Controls library for Grbl
 *
 * v0.02 / 2019-10-04 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2019, Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CNC.View;

namespace CNC.Controls
{
    public static class ProfileData
    {
        public static int dataId = -1, GrblToolNumber = -1;
        private static DataTable data;
        private static string profileFile = null;

        static ProfileData()
        {
            data = new DataTable("Profile");

            data.Columns.Add("ProfileId", typeof(int));
            data.Columns.Add("ToolNumber", typeof(int));
            data.Columns.Add("Name", typeof(string));
            data.Columns.Add("Mode", typeof(string));
            data.Columns.Add("IsEngraving", typeof(bool));
            data.Columns.Add("Speed", typeof(int));
            data.Columns.Add("MinPower", typeof(int));
            data.Columns.Add("Power", typeof(int));
            data.Columns.Add("DPI", typeof(int));
            data.Columns.Add("DutyCycle", typeof(int));
            data.Columns.Add("PPI", typeof(int));
            data.Columns.Add("PulseWidth", typeof(int));
            data.Columns.Add("AirAssist", typeof(bool));
            data.Columns.Add("Exhaust", typeof(bool));
            data.PrimaryKey = new DataColumn[] { data.Columns["ProfileId"] };
        }

        public static DataTable Data { get { return data; } }

        public static void Load(string profileFile)
        {
            bool updated = false;

            FileInfo file = new FileInfo(profileFile);
            if (file.Exists)
            {
                data.ReadXml(file.FullName);
            }

            ProfileData.profileFile = profileFile;

            if (data.Rows.Count == 0)
            {
                data.Columns.Remove("IsEngraving");
                data.Rows.Add(new object[] { ++dataId, null, "<default>", ViewType.Engraving.ToString(), 100, 10, 0, 600, 50, 600, 0, false, false });
                data.Rows.Add(new object[] { ++dataId, null, "<default>", ViewType.Mach3.ToString(), 100, 10, 0, 600, 25, 600, 2500, false, false });
                data.Rows.Add(new object[] { ++dataId, ++GrblToolNumber, "<default>", ViewType.GRBL.ToString(), 100, 10, 0, 600, 25, 600, 2500, false, false });
            }
            else
            {
                dataId = (int)data.Rows[data.Rows.Count - 1]["ProfileId"];
                foreach (DataRow row in data.Rows)
                {
                    if ((string)row["Mode"] == "GRBL")
                    {
                        if (row.IsNull("ToolNumber"))
                        {
                            updated = true;
                            row["ToolNumber"] = ++GrblToolNumber;
                        }
                        else
                            GrblToolNumber = Math.Max(GrblToolNumber, (int)(row["ToolNumber"]));
                    }
                    if (row.IsNull("MinPower"))
                        row["MinPower"] = 0;
                    if (row.IsNull("Exhaust"))
                        row["Exhaust"] = true;
                    if (row.IsNull("Mode"))
                        row["Mode"] = bool.Parse(row["IsEngraving"].ToString()) ? ViewType.Engraving.ToString() : ViewType.Mach3.ToString();
                }
                data.Columns.Remove("IsEngraving");

                if (data.Select("Mode='" + ViewType.GRBL.ToString() + "'").Count() == 0)
                    data.Rows.Add(new object[] { dataId++, 0, "<default>", ViewType.GRBL.ToString(), 100, 0, 10, 600, 25, 600, 2500, false, false });

                if (updated)
                    Save();

            }
        }

        public static void Save()
        {
            if (profileFile != null)
                data.WriteXml(profileFile);
        }
    }
}
