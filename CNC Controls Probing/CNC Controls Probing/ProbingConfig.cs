/*
 * ProbingProfiles.cs - part of CNC Probing library
 *
 * v0.29 / 2021-01-14 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2021, Io Engineering (Terje Io)
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace CNC.Controls.Probing
{
    [Serializable]
    public class ProbingProfile
    {
        [XmlIgnore]
        public int Id { get; set; }

        public string Name { get; set; }
        public double Offset { get; set; }
        public double XYClearance { get; set; }
        public double Depth { get; set; }
        public double ProbeDistance { get; set; }
        public double LatchDistance { get; set; }
        public double DistanceZ { get; set; }
        public double ProbeFeedRate { get; set; }
        public double LatchFeedRate { get; set; }
        public double RapidsFeedRate { get; set; }
        public double ProbeDiameter { get; set; }
        public double TouchPlateHeight { get; set; }
        public double FixtureHeight { get; set; }
        public double ProbeOffsetX { get; set; }
        public double ProbeOffsetY { get; set; }
    }

    public class ProbingProfiles
    {
        private int id = 0;

        public ObservableCollection<ProbingProfile> Profiles { get; private set; } = new ObservableCollection<ProbingProfile>();

        public int Add(string name, ProbingViewModel data)
        {
            Profiles.Add(new ProbingProfile
            {
                Id = id++,
                Name = name,
                RapidsFeedRate = data.RapidsFeedRate,
                ProbeFeedRate = data.ProbeFeedRate,
                LatchFeedRate = data.LatchFeedRate,
                ProbeDistance = data.ProbeDistance,
                LatchDistance = data.LatchDistance,
                ProbeDiameter = data.ProbeDiameter,
                Offset = data.Offset,
                ProbeOffsetX = data.ProbeOffsetX,
                ProbeOffsetY = data.ProbeOffsetY,
                XYClearance = data.XYClearance,
                Depth = data.Depth,
                TouchPlateHeight = data.TouchPlateHeight,
                FixtureHeight = data.FixtureHeight
            });

            return id - 1;
        }

        public void Update(int id, string name, ProbingViewModel data)
        {
            var profile = Profiles.Where(x => x.Id == id).FirstOrDefault();

            if (profile != null)
            {
                profile.Name = name;
                profile.RapidsFeedRate = data.RapidsFeedRate;
                profile.ProbeFeedRate = data.ProbeFeedRate;
                profile.LatchFeedRate = data.LatchFeedRate;
                profile.ProbeDistance = data.ProbeDistance;
                profile.LatchDistance = data.LatchDistance;
                profile.ProbeDiameter = data.ProbeDiameter;
                profile.Offset = data.Offset;
                profile.ProbeOffsetX = data.ProbeOffsetX;
                profile.ProbeOffsetY = data.ProbeOffsetY;
                profile.XYClearance = data.XYClearance;
                profile.Depth = data.Depth;
                profile.TouchPlateHeight = data.TouchPlateHeight;
                profile.FixtureHeight = data.FixtureHeight;
            }
        }

        public bool Delete(int id)
        {
            bool deleted = false;
            var profile = Profiles.Where(x => x.Id == id).FirstOrDefault();

            if (profile != null && Profiles.Count > 1)
                deleted = Profiles.Remove(profile);

            return deleted;
        }

        public void Save()
        {
            XmlSerializer xs = new XmlSerializer(typeof(ObservableCollection<ProbingProfile>));
            try
            {
                FileStream fsout = new FileStream(Core.Resources.Path + "ProbingProfiles.xml", FileMode.Create, FileAccess.Write, FileShare.None);
                using (fsout)
                {
                    xs.Serialize(fsout, Profiles);
                }
            }
            catch
            {
            }
        }

        public void Load()
        {
            XmlSerializer xs = new XmlSerializer(typeof(ObservableCollection<ProbingProfile>));

            try
            {
                StreamReader reader = new StreamReader(Core.Resources.Path + "ProbingProfiles.xml");
                Profiles = (ObservableCollection<ProbingProfile>)xs.Deserialize(reader);
                reader.Close();

                foreach (ProbingProfile profile in Profiles)
                    profile.Id = id++;
            }
            catch
            {
            }

            if (Profiles.Count == 0)
            {
                Profiles.Add(new ProbingProfile
                {
                    Id = id++,
                    Name = "<Default>",
                    RapidsFeedRate = 0d,
                    ProbeFeedRate = 100d,
                    LatchFeedRate = 25d,
                    ProbeDistance = 10d,
                    LatchDistance = .5d,
                    ProbeDiameter = 2d,
                    XYClearance = 5d,
                    Offset = 5d,
                    ProbeOffsetX = 0d,
                    ProbeOffsetY = 0d,
                    Depth = 3d,
                    TouchPlateHeight = 1d,
                    FixtureHeight = 1d
                });
            }
        }
    }
}
