/*
 * ThreadData.cs - part of CNC Controls library for Grbl
 *
 * v2.00 / 2019-09-27 / Io Engineering (Terje Io)
 *
 */

/*
 * Parameters extracted from original code by Stephan Brunker (written in FreeBasic)
 *
 * Project Homepage:
 * www.sourceforge.net/p/mach3threadinghelper 
 * 
 */

/*

Additional code:

Copyright (c) 2019, Io Engineering (Terje Io)
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

using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Globalization;
using System;

namespace CNC.Controls.Lathe
{
    class ThreadData
    {

        public static double[] TUN = { 0.06, 0.073, 0.086, 0.099, 0.112, 0.125, 0.138, double.NaN, 0.164, double.NaN, 0.190, double.NaN, 0.216 };

        public ThreadData()
        {
        }

        #region ThreadData

        public void AddThreads()
        {
            using (new UIUtils.WaitCursor())
            {
                Thread.Type type = Thread.Type.M_6G6H;

                Thread.AddType(type, "M - Metric ISO Thread medium 6g/6H", true);
                Thread.Add(type, "M 3 x 0,5");
                Thread.Add(type, "M 4 x 0,7");
                Thread.Add(type, "M 5 x 0,8");
                Thread.Add(type, "M 6 x 1");
                Thread.Add(type, "M 8 x 1,25");
                Thread.Add(type, "M 10 x 1,5");
                Thread.Add(type, "M 12 x 1,75");
                Thread.Add(type, "M 16 x 2");
                Thread.Add(type, "M 20 x 2,5");
                Thread.Add(type, "M 24 x 3");
                Thread.Add(type, "M 30 x 3,5");
                Thread.Add(type, "M 36 x 4");
                Thread.Add(type, "M 42 x 4,5");
                Thread.Add(type, "M 48 x 5");
                Thread.Add(type, "M 56 x 5,5");
                Thread.Add(type, "M 64 x 6");

                type = Thread.Type.M_4G4H;

                Thread.AddType(type, "M - Metric ISO Thread fine 4g/4H", true);
                Thread.Add(type, "M 4 x 0,35");
                Thread.Add(type, "M 4 x 0,5");
                Thread.Add(type, "M 5 x 0,5");
                Thread.Add(type, "M 6 x 0,5");
                Thread.Add(type, "M 6 x 0,75");
                Thread.Add(type, "M 8 x 0,5");
                Thread.Add(type, "M 8 x 0,75");
                Thread.Add(type, "M 8 x 1");
                Thread.Add(type, "M 10 x 0,5");
                Thread.Add(type, "M 10 x 0,75");
                Thread.Add(type, "M 10 x 1");
                Thread.Add(type, "M 10 x 1,25");
                Thread.Add(type, "M 12 x 0,5");
                Thread.Add(type, "M 12 x 1");
                Thread.Add(type, "M 12 x 1,25");
                Thread.Add(type, "M 12 x 1,5");
                Thread.Add(type, "M 16 x 1");
                Thread.Add(type, "M 16 x 1,5");
                Thread.Add(type, "M 20 x 1,5");
                Thread.Add(type, "M 20 x 2");
                Thread.Add(type, "M 24 x 1,5");
                Thread.Add(type, "M 24 x 2");
                Thread.Add(type, "M 30 x 1,5");
                Thread.Add(type, "M 30 x 2");
                Thread.Add(type, "M 30 x 3");

                type = Thread.Type.M_KEG_L;

                Thread.AddType(type, "M Keg - Metric Tapered (Bolt only)", true);
                Thread.Add(type, "M 5 x 0,8 keg");
                Thread.Add(type, "M 6 x 1 keg");
                Thread.Add(type, "M 8 x 1 keg");
                Thread.Add(type, "M 10 x 1 keg");
                Thread.Add(type, "M 10 x 1,25 keg");
                Thread.Add(type, "M 12 x 1 keg");
                Thread.Add(type, "M 12 x 1,25 keg");
                Thread.Add(type, "M 12 x 1,5 keg");
                Thread.Add(type, "M 14 x 1,5 keg");
                Thread.Add(type, "M 16 x 1,5 keg");
                Thread.Add(type, "M 18 x 1,5 keg");
                Thread.Add(type, "M 20 x 1,5 keg");
                Thread.Add(type, "M 22 x 1,5 keg");
                Thread.Add(type, "M 24 x 1,5 keg");
                Thread.Add(type, "M 26 x 1,5 keg");
                Thread.Add(type, "M 27 x 1,5 keg");
                Thread.Add(type, "M 30 x 1,5 keg");

                type = Thread.Type.M_KEG_K;

                Thread.AddType(type, "M Keg Short - Short version", true);

                DataRow[] rows = Thread.data.Select("type = " + (int)Thread.Type.M_KEG_L);
                foreach (DataRow row in rows)
                {
                    if ((string)row["name"] != "M 5 x 0.8 keg")
                        Thread.Add(type, (string)row["name"], row);
                }

                type = Thread.Type.G_A;

                Thread.AddType(type, "G - Withworth Pipe Thread (Class A)", false);
                Thread.Add(type, "G 1/16", 7.723d, 0.214d, 0.282d, 0.107d, 28);
                Thread.Add(type, "G 1/8", 9.728, 0.214, 0.282, 0.107, 28);
                Thread.Add(type, "G 1/4", 13.157, 0.250, 0.445, 0.125, 19);
                Thread.Add(type, "G 3/8", 16.662, 0.250, 0.445, 0.125, 19);
                Thread.Add(type, "G 1/2", 20.955, 0.284, 0.541, 0.142, 14);
                Thread.Add(type, "G 5/8", 22.911, 0.284, 0.541, 0.142, 14);
                Thread.Add(type, "G 3/4", 26.441, 0.284, 0.541, 0.142, 14);
                Thread.Add(type, "G 7/8", 30.201, 0.284, 0.541, 0.142, 14);
                Thread.Add(type, "G 1''", 33.249, 0.360, 0.640, 0.180, 11);
                Thread.Add(type, "G 1 1/8", 37.892, 0.360, 0.640, 0.180, 11);
                Thread.Add(type, "G 1 1/4", 41.910, 0.360, 0.640, 0.180, 11);
                Thread.Add(type, "G 1 1/2", 47.803, 0.360, 0.640, 0.180, 11);
                Thread.Add(type, "G 1 3/4", 53.746, 0.360, 0.640, 0.180, 11);
                Thread.Add(type, "G 2''", 59.614, 0.360, 0.640, 0.180, 11);
                Thread.Add(type, "G 2 1/4", 65.710, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 2 1/2", 75.184, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 2 3/4", 81.534, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 3''", 87.884, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 3 1/3", 100.330, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 4''", 113.030, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 4 1/2", 125.730, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 5''", 138.430, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 5 1/2", 151.130, 0.434, 0.640, 0.217, 11);
                Thread.Add(type, "G 6''", 163.830, 0.434, 0.640, 0.217, 11);

                type = Thread.Type.R;

                Thread.AddType(type, "R/Rc - Withworth Pipe Tapered", true);
                Thread.Add(type, "R 1/16", 7.723, 28, 4, 7.4);
                Thread.Add(type, "R 1/8", 9.728, 28, 4, 7.4);
                Thread.Add(type, "R 1/4", 13.157, 19, 6, 11);
                Thread.Add(type, "R 3/8", 16.662, 19, 6.4, 11.4);
                Thread.Add(type, "R 1/2", 20.955, 14, 8.2, 15);
                Thread.Add(type, "R 3/4", 26.441, 14, 9.5, 16.3);
                Thread.Add(type, "R 1''", 33.249, 11, 10.4, 19.1);
                Thread.Add(type, "R 1 1/4", 41.91, 11, 12.7, 21.4);
                Thread.Add(type, "R 1 1/2", 47.803, 11, 12.7, 21.4);
                Thread.Add(type, "R 2''", 59.614, 11, 15.9, 25.7);
                Thread.Add(type, "R 2 1/2", 75.184, 11, 17.5, 30.2);
                Thread.Add(type, "R 3''", 87.884, 11, 20.6, 33.3);
                Thread.Add(type, "R 4''", 113.03, 11, 25.4, 39.3);
                Thread.Add(type, "R 5''", 138.43, 11, 28.6, 43.6);
                Thread.Add(type, "R 6''", 163.83, 11, 28.6, 43.6);

                type = Thread.Type.BSW;

                Thread.AddType(type, "BSW - Withworth (medium class)", true);
                Thread.Add(type, " 1/8  - 40 BSW");
                Thread.Add(type, " 3/16 - 24 BSW");
                Thread.Add(type, " 1/4  - 20 BSW");
                Thread.Add(type, " 5/16 - 18 BSW");
                Thread.Add(type, " 3/8  - 16 BSW");
                Thread.Add(type, " 7/16 - 14 BSW");
                Thread.Add(type, " 1/2  - 12 BSW");
                Thread.Add(type, " 9/16 - 12 BSW");
                Thread.Add(type, " 5/8  - 11 BSW");
                Thread.Add(type, "11/16 - 11 BSW");
                Thread.Add(type, " 3/4  - 10 BSW");
                Thread.Add(type, " 7/8  - 9  BSW");
                Thread.Add(type, "1''   - 8  BSW");
                Thread.Add(type, "1 1/8 - 7  BSW");
                Thread.Add(type, "1 1/4 - 7  BSW");
                Thread.Add(type, "1 1/2 - 6  BSW");
                Thread.Add(type, "1 3/4 - 5  BSW");
                Thread.Add(type, "2''   - 4 1/2 BSW");
                Thread.Add(type, "2 1/4 - 4     BSW");
                Thread.Add(type, "2 1/2 - 4     BSW");
                Thread.Add(type, "2 3/4 - 3 1/2 BSW");
                Thread.Add(type, "3''   - 3 1/2 BSW");
                Thread.Add(type, "3 1/4 - 3 1/4 BSW");
                Thread.Add(type, "3 1/2 - 3 1/4 BSW");
                Thread.Add(type, "3 3/4 - 3     BSW");
                Thread.Add(type, "4''   - 3     BSW");
                Thread.Add(type, "4 1/2 - 2 7/8 BSW");
                Thread.Add(type, "5''   - 2 3/4 BSW");
                Thread.Add(type, "5 1/2 - 2 5/8 BSW");
                Thread.Add(type, "6''   - 2 1/2 BSW");

                type = Thread.Type.BSF;

                Thread.AddType(type, "BSF - Withworth (fine class)", true);
                Thread.Add(type, " 3/16 - 32 BSF");
                Thread.Add(type, " 1/4  - 26 BSF");
                Thread.Add(type, " 5/16 - 22 BSF");
                Thread.Add(type, " 3/8  - 20 BSF");
                Thread.Add(type, " 7/16 - 18 BSF");
                Thread.Add(type, " 1/2  - 16 BSF");
                Thread.Add(type, " 9/16 - 16 BSF");
                Thread.Add(type, " 5/8  - 14 BSF");
                Thread.Add(type, " 3/4  - 12 BSF");
                Thread.Add(type, " 7/8  - 11 BSF");
                Thread.Add(type, "1''   - 10 BSF");
                Thread.Add(type, "1 1/8 - 9  BSF");
                Thread.Add(type, "1 1/4 - 9  BSF");
                Thread.Add(type, "1 1/2 - 8  BSF");
                Thread.Add(type, "1 3/4 - 7  BSF");
                Thread.Add(type, "2''   - 7  BSF");
                Thread.Add(type, "2 1/4 - 6  BSF");
                Thread.Add(type, "2 1/2 - 6  BSF");
                Thread.Add(type, "2 3/4 - 6  BSF");
                Thread.Add(type, "3''   - 5  BSF");
                Thread.Add(type, "3 1/4 - 5  BSF");
                Thread.Add(type, "3 1/2 - 4 1/2 BSF");
                Thread.Add(type, "3 3/4 - 4 1/2 BSF");
                Thread.Add(type, "4''   - 4 1/2 BSF");
                Thread.Add(type, "4 1/4 - 4     BSF");

                type = Thread.Type.UNC_2;

                Thread.AddType(type, "UNC - Unified Thread Tol. 2A/2B (medium)", true);
                Thread.Add(type, "   #5 - 40 UNC");
                Thread.Add(type, "   #6 - 32 UNC");
                Thread.Add(type, "   #8 - 32 UNC");
                Thread.Add(type, "  #10 - 24 UNC");
                Thread.Add(type, "  #12 - 24 UNC");
                Thread.Add(type, "  1/4 - 20 UNC");
                Thread.Add(type, " 5/16 - 18 UNC");
                Thread.Add(type, "  3/8 - 16 UNC");
                Thread.Add(type, " 7/16 - 14 UNC");
                Thread.Add(type, "  1/2 - 13 UNC");
                Thread.Add(type, " 9/16 - 12 UNC");
                Thread.Add(type, "  5/8 - 11 UNC");
                Thread.Add(type, "  3/4 - 10 UNC");
                Thread.Add(type, "  7/8 -  9 UNC");
                Thread.Add(type, "1''   -  8 UNC");
                Thread.Add(type, "1 1/8 -  7 UNC");
                Thread.Add(type, "1 1/4 -  7 UNC");
                Thread.Add(type, "1 3/8 -  6 UNC");
                Thread.Add(type, "1 1/2 -  6 UNC");
                Thread.Add(type, "1 3/4 -  5 UNC");
                Thread.Add(type, "2''   - 4 1/2 UNC");
                Thread.Add(type, "2 1/4 - 4 1/2 UNC");
                Thread.Add(type, "2 1/2 -  4 UNC");
                Thread.Add(type, "2 3/4 -  4 UNC");
                Thread.Add(type, "3''   -  4 UNC");
                Thread.Add(type, "3 1/4 -  4 UNC");
                Thread.Add(type, "3 1/2 -  4 UNC");
                Thread.Add(type, "3 3/4 -  4 UNC");
                Thread.Add(type, "4''   -  4 UNC");

                type = Thread.Type.UNC_3;

                Thread.AddType(type, "UNC - Unified Thread Tol. 3A/3B (fine)", true);

                rows = Thread.data.Select("type = " + (int)Thread.Type.UNC_2);
                foreach (DataRow row in rows)
                {
                    Thread.Add(type, (string)row["name"], row);
                }

                type = Thread.Type.UNF_2;

                Thread.AddType(type, "UNF - Unified Fine Thread Tol. 2A/2B (medium)", true);
                Thread.Add(type, "   #5 - 44 UNF");
                Thread.Add(type, "   #6 - 40 UNF");
                Thread.Add(type, "   #8 - 36 UNF");
                Thread.Add(type, "  #10 - 32 UNF");
                Thread.Add(type, "  #12 - 28 UNF");
                Thread.Add(type, "  1/4 - 28 UNF");
                Thread.Add(type, " 5/16 - 24 UNF");
                Thread.Add(type, "  3/8 - 24 UNF");
                Thread.Add(type, " 7/16 - 10 UNF");
                Thread.Add(type, "  1/2 - 20 UNF");
                Thread.Add(type, " 9/16 - 18 UNF");
                Thread.Add(type, "  5/8 - 18 UNF");
                Thread.Add(type, "  3/4 - 16 UNF");
                Thread.Add(type, "  7/8 - 14 UNF");
                Thread.Add(type, "1''   - 12 UNF");
                Thread.Add(type, "1 1/8 - 12 UNF");
                Thread.Add(type, "1 1/4 - 12 UNF");
                Thread.Add(type, "1 3/8 - 12 UNF");
                Thread.Add(type, "1 1/2 - 12 UNF");

                type = Thread.Type.UNF_3;

                Thread.AddType(type, "UNF - Unified Fine Thread Tol. 3A/3B (fine)", true);

                rows = Thread.data.Select("type = " + (int)Thread.Type.UNF_2);
                foreach (DataRow row in rows)
                {
                    Thread.Add(type, (string)row["name"], row);
                }

                type = Thread.Type.UNEF_2;

                Thread.AddType(type, "UNEF - Unified Extra Fine Thread Tol. 2A/2B (medium)", true);
                Thread.Add(type, " #12  - 32 UNEF");
                Thread.Add(type, " 1/4  - 32 UNEF");
                Thread.Add(type, " 5/16 - 32 UNEF");
                Thread.Add(type, " 3/8  - 32 UNEF");
                Thread.Add(type, " 7/16 - 28 UNEF");
                Thread.Add(type, " 1/2  - 28 UNEF");
                Thread.Add(type, " 9/16 - 24 UNEF");
                Thread.Add(type, " 5/8  - 24 UNEF");
                Thread.Add(type, "11/16 - 24 UNEF");
                Thread.Add(type, " 3/4  - 20 UNEF");
                Thread.Add(type, "13/16 - 20 UNEF");
                Thread.Add(type, " 7/8  - 20 UNEF");
                Thread.Add(type, "15/16 - 20 UNEF");
                Thread.Add(type, "1''   - 20 UNEF");
                Thread.Add(type, "1 1/16 - 18 UNEF");
                Thread.Add(type, "1 1/8  - 18 UNEF");
                Thread.Add(type, "1 3/16 - 18 UNEF");
                Thread.Add(type, "1 1/4  - 18 UNEF");
                Thread.Add(type, "1 5/16 - 18 UNEF");
                Thread.Add(type, "1 3/8  - 18 UNEF");
                Thread.Add(type, "1 7/16 - 18 UNEF");
                Thread.Add(type, "1 1/2  - 18 UNEF");
                Thread.Add(type, "1 9/16 - 18 UNEF");
                Thread.Add(type, "1 5/8  - 18 UNEF");
                Thread.Add(type, "1 11/16 - 18 UNEF");

                type = Thread.Type.UNEF_3;

                Thread.AddType(type, "UNEF - Unified Extra Fine Thread Tol. 3A/3B (fine)", true);

                rows = Thread.data.Select("type = " + (int)Thread.Type.UNEF_2);
                foreach (DataRow row in rows)
                {
                    Thread.Add(type, (string)row["name"], row);
                }

                type = Thread.Type.NPSM;

                Thread.AddType(type, "NPSM - Nominal Pipe Size Thread", false);
                Thread.Add(type, " 1/8 - 27 NPSM", 10.084, 9.093, 0.153, 0.178, 0.12, 0.092);
                Thread.Add(type, " 1/4 - 18 NPSM", 13.36, 11.887, 0.33, 0.228, 0.147, 0.111);
                Thread.Add(type, " 3/8 - 18 NPSM", 16.815, 15.316, 0.229, 0.229, 0.15, 0.114);
                Thread.Add(type, " 1/2 - 14 NPSM", 20.904, 18.974, 0.305, 0.254, 0.171, 0.129);
                Thread.Add(type, " 3/4 - 14 NPSM", 26.264, 24.333, 0.305, 0.254, 0.175, 0.134);
                Thread.Add(type, "1''   - 11 1/2 NPSM", 32.842, 30.505, 0.254, 0.305, 0.193, 0.148);
                Thread.Add(type, "1 1/4 - 11 1/2 NPSM", 41.605, 39.268, 0.229, 0.305, 0.198, 0.153);
                Thread.Add(type, "1 1/2 - 11 1/2 NPSM", 47.676, 45.339, 0.229, 0.305, 0.201, 0.155);
                Thread.Add(type, "2''   - 11 1/2 NPSM", 59.715, 57.379, 0.228, 0.304, 0.206, 0.158);
                Thread.Add(type, "2 1/2 - 8 NPSM", 72.161, 68.783, 0.483, 0.381, 0.249, 0.188);
                Thread.Add(type, "3''   - 8 NPSM", 88.062, 84.684, 0.482, 0.381, 0.251, 0.193);
                Thread.Add(type, "3 1/2 - 8 NPSM", 100.787, 97.409, 0.33, 0.381, 0.254, 0.195);
                Thread.Add(type, "4''   - 8 NPSM", 113.436, 110.058, 0.33, 0.381, 0.254, 0.196);
                Thread.Add(type, "5''   - 8 NPSM", 140.411, 137.033, 0.33, 0.381, 0.267, 0.2);
                Thread.Add(type, "6''   - 8 NPSM", 167.259, 163.881, 0.305, 0.381, 0.267, 0.205);

                type = Thread.Type.NPT;

                Thread.AddType(type, "NPT - National Pipe Thread Taper", false);
                Thread.Add(type, " 1/16 - 27 NPT", 27, 4.064, 7.142, 6.632, 7.302);
                Thread.Add(type, " 1/8  - 27 NPT", 27, 4.102, 9.489, 6.703, 9.652);
                Thread.Add(type, "  1/4 - 18 NPT", 18, 5.786, 12.487, 10.206, 12.761);
                Thread.Add(type, "  3/8 - 18 NPT", 18, 6.096, 15.926, 10.358, 16.192);
                Thread.Add(type, "  1/2 - 14 NPT", 14, 8.128, 19.772, 13.556, 20.111);
                Thread.Add(type, "  3/4 - 14 NPT", 14, 8.611, 25.117, 13.861, 25.445);
                Thread.Add(type, "1''   - 11 1/2 NPT", 11.5, 10.16, 31.461, 17.343, 31.910);
                Thread.Add(type, "1 1/4 - 11 1/2 NPT", 11.5, 10.668, 40.218, 17.953, 40.673);
                Thread.Add(type, "1 1/2 - 11 1/2 NPT", 11.5, 10.668, 46.287, 18.377, 46.769);
                Thread.Add(type, "2''   - 11 1/2 NPT", 11.5, 11.074, 58.325, 19.215, 58.834);
                Thread.Add(type, "2 1/2 - 8 NPT", 8, 17.323, 70.159, 28.893, 70.882);
                Thread.Add(type, "3''   - 8 NPT", 8, 19.456, 86.068, 30.480, 86.757);
                Thread.Add(type, "3 1/2 - 8 NPT", 8, 20.853, 98.776, 31.75, 99.457);
                Thread.Add(type, "4''  - 8 NPT", 8, 21.438, 111.433, 33.02, 112.157);
                Thread.Add(type, "5''  - 8 NPT", 8, 23.8, 138.412, 35.72, 139.157);
                Thread.Add(type, "6''  - 8 NPT", 8, 24.333, 165.252, 38.418, 166.132);
                Thread.Add(type, "8''  - 8 NPT", 8, 27, 215.901, 43.498, 216.932);
                Thread.Add(type, "10'' - 8 NPT", 8, 30.734, 269.772, 48.895, 270.907);
                Thread.Add(type, "11'' - 8 NPT", 8, 34.544, 320.492, 53.975, 321.707);

                Thread.AddTPIMap(type, 27, 0.03, 0.091, 0.03, 0.091);
                Thread.AddTPIMap(type, 18, 0.046, 0.124, 0.046, 0.124);
                Thread.AddTPIMap(type, 14, 0.061, 0.142, 0.061, 0.142);
                Thread.AddTPIMap(type, 11.5, 0.074, 0.160, 0.074, 0.160);
                Thread.AddTPIMap(type, 8, 0.104, 0.198, 0.104, 0.198);

                type = Thread.Type.NPSC;
                Thread.AddType(type, "NPSC - Inside Thread for combination with NPT", false);

                Thread.Add(type, " 1/8 - 27 NPSC", 9.490, 0.177);
                Thread.Add(type, " 1/4 - 18 NPSC", 12.487, 0.264);
                Thread.Add(type, " 3/8 - 18 NPSC", 15.926, 0.264);
                Thread.Add(type, " 1/2 - 14 NPSC", 19.772, 0.341);
                Thread.Add(type, " 3/4 - 14 NPSC", 25.118, 0.34);
                Thread.Add(type, " 1''  - 11 1/2 NPSC", 31.462, 0.414);
                Thread.Add(type, "1 1/4 - 11 1/2 NPSC", 40.217, 0.414);
                Thread.Add(type, "1 1/2 - 11 1/2 NPSC", 46.288, 0.414);
                Thread.Add(type, " 2''  - 11 1/2 NPSC", 58.325, 0.414);
                Thread.Add(type, "2 1/2 - 8 NPSC", 70.159, 0.597);
                Thread.Add(type, " 3''  - 8 NPSC", 86.068, 0.594);
                Thread.Add(type, "3 1/2 - 8 NPSCM", 98.776, 0.595);
                Thread.Add(type, " 4''  - 8 NPSC", 111.433, 0.595);

                type = Thread.Type.NPTF;

                Thread.AddType(type, "NPTF - National Pipe Thread Fuel", false);

                uint i = 0;

                rows = Thread.data.Select("type = " + (int)Thread.Type.NPT);
                foreach (DataRow row in rows)
                {
                    i++;
                    if (i <= 12)
                        Thread.Add(type, ((string)row["name"]).Replace("NPT", "NPTF"), row);
                }

                Thread.AddTPIMap(type, 27, 0.043, 0.089, 0.089, 0.132);
                Thread.AddTPIMap(type, 18, 0.066, 0.109, 0.109, 0.155);
                Thread.AddTPIMap(type, 14, 0.066, 0.109, 0.109, 0.155);
                Thread.AddTPIMap(type, 11.5, 0.089, 0.132, 0.132, 0.198);
                Thread.AddTPIMap(type, 8, 0.132, 0.175, 0.175, 0.241);

                type = Thread.Type.NPSF;
                Thread.AddType(type, "NPSF - Inside Thread for combination with NPSF", false);
                Thread.Add(type, " 1/16 - 27 NPSF", 7.076, 0.089);
                Thread.Add(type, " 1/8 - 27 NPSF", 9.423, 0.089);
                Thread.Add(type, " 1/4 - 18 NPSF", 12.390, 0.122);
                Thread.Add(type, " 3/8 - 18 NPSF", 15.822, 0.122);
                Thread.Add(type, " 1/2 - 14 NPSF", 19.643, 0.170);
                Thread.Add(type, " 3/4 - 14 NPSF", 24.990, 0.170);
                Thread.Add(type, " 1''  - 11 1/2 NPSF", 31.304, 0.206);

            }
        }

        #endregion
    }

    public static class Thread
    {
        public enum Type
        {
            M_6G6H,
            M_4G4H,
            M_KEG_L,
            M_KEG_K,
            G_A,
            R,
            BSW,
            BSF,
            UNC_2,
            UNC_3,
            UNF_2,
            UNF_3,
            UNEF_2,
            UNEF_3,
            NPSM,
            NPT,
            NPSC,
            NPTF,
            NPSF
        }

        public enum Toolshape
        {
            Rounded,
            Chamfer
        }

        public enum Format
        {
            LinuxCNC,
            Mach3Native,
            Mach3Sandvik
        }

        [Flags]
        public enum Side : int
        {
            Outside = 0x01,
            Inside  = 0x02,
            Both = 0x03
        }
                                                                           
        public static DataTable data;
        public static DataTable tpimap;
        public static Dictionary<Thread.Type, string> type = new Dictionary<Thread.Type, string>();
        private static int id = 0;

        static Thread()
        {
            Thread.data = new DataTable("Thread");

            Thread.data.Columns.Add("Id", typeof(int));
            Thread.data.Columns.Add("Type", typeof(Thread.Type));
            Thread.data.Columns.Add("Name", typeof(string));
            Thread.data.Columns.Add("Free", typeof(bool));
            Thread.data.Columns.Add("v1", typeof(double));
            Thread.data.Columns.Add("v2", typeof(double));
            Thread.data.Columns.Add("v3", typeof(double));
            Thread.data.Columns.Add("v4", typeof(double));
            Thread.data.Columns.Add("v5", typeof(double));
            Thread.data.Columns.Add("v6", typeof(double));
            Thread.data.PrimaryKey = new DataColumn[] { Thread.data.Columns["Id"] };

            Thread.tpimap = new DataTable("Thread");

            Thread.tpimap.Columns.Add("Type", typeof(Thread.Type));
            Thread.tpimap.Columns.Add("tpi", typeof(double));
            Thread.tpimap.Columns.Add("smin", typeof(double));
            Thread.tpimap.Columns.Add("smax", typeof(double));
            Thread.tpimap.Columns.Add("gmin", typeof(double));
            Thread.tpimap.Columns.Add("gmax", typeof(double));
            Thread.tpimap.PrimaryKey = new DataColumn[] { Thread.tpimap.Columns["Type"], Thread.tpimap.Columns["tpi"] };
        }

        // No parameter values
        public static void Add(Thread.Type type, string name)
        {
            Thread.data.Rows.Add(new object[] { ++Thread.id, type, name.Replace(',', '.'), false });
        }

        // G
        public static void Add(Thread.Type type, string name, double dia, double tda, double tdi, double tdf, uint tpi)
        {
            Thread.data.Rows.Add(new object[] { ++Thread.id, type, name.Replace(',', '.'), false, dia, tda, tdi, tdf, tpi });
        }

        // NPSM
        public static void Add(Thread.Type type, string name, double da, double kd, double tdi, double tda, double tfi, double tfa)
        {
            Thread.data.Rows.Add(new object[] { ++Thread.id, type, name.Replace(',', '.'), false, da, kd, tda, tdi, tfi, tfa });
        }

        // NPT
        public static void Add(Thread.Type type, string name, double tpi, double pli, double fdi, double pla, double fda)
        {
            Thread.data.Rows.Add(new object[] { ++Thread.id, type, name.Replace(',', '.'), false, tpi, pli, pla, fdi, fda });
        }

        // NPSC
        public static void Add(Thread.Type type, string name, double fd, double tolf)
        {
            Thread.data.Rows.Add(new object[] { ++Thread.id, type, name.Replace(',', '.'), false, fd, tolf });
        }

        // R
        public static void Add(Thread.Type type, string name, double dia, uint tpi, double pl, double tl)
        {
            Thread.data.Rows.Add(new object[] { ++Thread.id, type, name.Replace(',', '.'), false, dia, tpi, pl, tl });
        }

        public static DataRow Add(Thread.Type type, string tname, DataRow row)
        {
            if ((string)row["name"] != "")
            {
                DataRow newr = Thread.data.NewRow();
                newr.ItemArray = row.ItemArray;
                newr["Id"] = ++Thread.id;
                newr["Type"] = type;
                newr["name"] = tname;
                Thread.data.Rows.Add(newr);
                return newr;
            }
            return null;
        }

        public static void AddType(Thread.Type type, string tname, bool free)
        {
            Thread.type.Add(type, tname);
            if (free)
                Thread.data.Rows.Add(new object[] { ++Thread.id, type, "", true });
        }

        public static void AddTPIMap(Thread.Type type, double tpi, double smin, double smax, double gmin, double gmax)
        {
            Thread.tpimap.Rows.Add(new object[] { type, tpi, smin, smax, gmin, gmax });
        }
    }

    public class ThreadR
    {
        public ThreadR(DataRow selection)
        {
            dia = (double)selection["v1"];
            tpi = (double)selection["v2"];
            pl = (double)selection["v3"];
            tl = (double)selection["v4"];
        }

        public double dia { get; private set; }
        public double tpi { get; private set; }
        public double pl { get; private set; }
        public double tl { get; private set; }
    }

    public class ThreadNPSC
    {
        public ThreadNPSC(DataRow selection)
        {
            fd = (double)selection["v1"];
            tolf = (double)selection["v2"];
        }

        public double fd { get; private set; }
        public double tolf { get; private set; }
    }

    public class ThreadG
    {
        public ThreadG(DataRow selection)
        {
            dia = (double)selection["v1"];
            tda = (double)selection["v2"];
            tdi = (double)selection["v3"];
            tdf = (double)selection["v4"];
            tpi = (double)selection["v5"];
        }

        public double dia { get; private set; }
        public double tda { get; private set; }
        public double tdi { get; private set; }
        public double tdf { get; private set; }
        public double tpi { get; private set; }
    }

    public class ThreadNPSM
    {
        public ThreadNPSM(DataRow selection)
        {
            da = (double)selection["v1"];
            kd = (double)selection["v2"];
            tda = (double)selection["v3"];
            tdi = (double)selection["v4"];
            tfi = (double)selection["v5"];
            tfa = (double)selection["v6"];
        }

        public double da { get; private set; }
        public double kd { get; private set; }
        public double tda { get; private set; }
        public double tdi { get; private set; }
        public double tfi { get; private set; }
        public double tfa { get; private set; }
    }

    public class ThreadNPT
    {
        public ThreadNPT(DataRow selection)
        {
            tpi = (double)selection["v1"];
            pli = (double)selection["v2"];
            pla = (double)selection["v3"];
            fdi = (double)selection["v4"];
            fda = (double)selection["v5"];
        }

        public double tpi { get; private set; }
        public double pli { get; private set; }
        public double pla { get; private set; }
        public double fdi { get; private set; }
        public double fda { get; private set; }
    }

    public class TAbfl
    {
        public TAbfl(Thread.Type ttype, double tpi)
        {
            string s = string.Format("Type = {0} and tpi = {1}", (int)ttype, tpi.ToString(CultureInfo.InvariantCulture));
            DataRow[] data = Thread.tpimap.Select(s);
            if ((found = data.Count() == 1))
            {
                smin = (double)data[0]["smin"];
                smax = (double)data[0]["smax"];
                gmin = (double)data[0]["gmin"];
                gmax = (double)data[0]["gmax"];
            }
        }

        public bool found { get; private set; }
        public double smin { get; private set; }
        public double smax { get; private set; }
        public double gmin { get; private set; }
        public double gmax { get; private set; }
    }
}