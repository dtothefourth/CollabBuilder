using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Compression;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;

namespace CollabBuilder
{
    public partial class Form1 : Form
    {
        private static string[] GFXSlots = {
            "AN2", "LT3",
            "BG3", "BG2", "FG3", "BG1", "FG2", "FG1",
            "SP4", "SP3", "SP2", "SP1",
            "LG4", "LG3", "LG2", "LG1"
        };

        private static int[] SharedGFX = new int[4096];

        private static bool compiling = false;

        public Form1()
        {
            InitializeComponent();
        }

        private string GetUberASM(string path, string level)
        {
            string uber = "";
            string line;
            string file;
            string[] words;

            path += "\\UberASM";

            if (!Directory.Exists(path))
            {
                return uber;
            }

            file = path;
            file += "\\list.txt";

            if (!File.Exists(file))
            {
                return uber;
            }

            bool valid = false;
            StreamReader text = new StreamReader(file);
            while ((line = text.ReadLine()) != null)
            {
                line = line.Split(';')[0];
                line = Regex.Replace(line, @"\s+", " ");
                line.Trim();
                if (line != "")
                {
                    words = line.Split(' ');

                    if (words[0].Contains(":"))
                    {
                        if (words[0] == "level:") valid = true;
                        else valid = false;
                    }

                    if (valid && words[0] == level)
                    {
                        uber = words[1];
                        text.Close();
                        return uber;
                    }
                }
            }
            text.Close();

            return uber;
        }

        private void GetSpriteBytes(string file, ref byte e1, ref byte e2)
        {

            //HANDLE JSON

            string line = "";
            string[] words;
            if (!File.Exists(file))
            {
                return;
            }

            StreamReader text = new StreamReader(file);

            for (int i = 0; i < 6; i++)
            {
                line = text.ReadLine();
                if (line == null) return;
            }
            words = line.Split(':');

            if (words.Length < 2) return;

            words[0] = Regex.Replace(words[0], @"\s+", " ");
            words[0].Trim();
            if (words[0] != "") {
                e1 = byte.Parse(words[0], System.Globalization.NumberStyles.HexNumber);
            }

            words[1] = Regex.Replace(words[1], @"\s+", " ");
            words[1].Trim();
            if (words[1] != "")
            {
                e2 = byte.Parse(words[1], System.Globalization.NumberStyles.HexNumber);
            }

            text.Close();
        }

        private string GetMWLLevel(string path)
        {
            string level = "";

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                int o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt16();
                level = o.ToString("X");
            }
            catch (Exception e)
            {

            }
            reader.Close();

            return level;
        }

        private string GetMWLGraphics(string path, string level, string sublevel, ref int gfx)
        {

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));
            String v;
            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                int o = reader.ReadInt32() + 8 * 7;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                bool bypass = true;
                bool l3bypass = true;
                bool ltbypass = true;

                for (int i = 0; i < GFXSlots.Length; i++)
                {
                    o = reader.ReadInt16();
                    if (i == 0 && (o & 0x8000) == 0) bypass = false;
                    if (i == 0 && (o & 0x4000) == 0) l3bypass = false;
                    if (i == 0 && (o & 0x2000) == 0) ltbypass = false;

                    if (!bypass)
                    {
                        if (i == 0 || (i > 1 && i < 12)) continue;
                    }
                    if (!l3bypass)
                    {
                        if (i >= 12 && i <= 15) continue;
                    }
                    if (!ltbypass)
                    {
                        if (i == 1) continue;
                    }

                    o = o & 0x0FFF;
                    if (o > 0x7F)
                    {
                        if (SharedGFX[o] == 0)
                        {
                            Graphics.Rows.Add(level, sublevel, GFXSlots[i], gfx.ToString("X"), o.ToString("X"), "");
                            gfx++;
                        }
                        else if (SharedGFX[o] == 1)
                        {
                            Graphics.Rows.Add(level, sublevel, GFXSlots[i], gfx.ToString("X"), o.ToString("X"), "Y");
                            gfx++;
                            SharedGFX[o]++;
                        }
                        else
                        {
                            for(int j = 0; j < Graphics.RowCount; j++)
                            {
                                if(o.ToString("X") == Graphics.Rows[j].Cells[4].Value.ToString())
                                {
                                    v = Graphics.Rows[j].Cells[3].Value.ToString();
                                    Graphics.Rows.Add(level, sublevel, GFXSlots[i], v, o.ToString("X"), "Y");
                                    break;
                                }
                            }
                        }

                    }
                }
            }
            catch (Exception e)
            {

            }
            reader.Close();

            return level;
        }


        private string SetMWLGraphics(string path, string level, string sublevel)
        {

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));

            String v;
            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                int o = reader.ReadInt32() + 8 * 7;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                bool bypass = true;
                bool l3bypass = true;
                bool ltbypass = true;

                for (int i = 0; i < GFXSlots.Length; i++)
                {
                    o = reader.ReadInt16();
                    if (i == 0 && (o & 0x8000) == 0) bypass = false;
                    if (i == 0 && (o & 0x4000) == 0) l3bypass = false;
                    if (i == 0 && (o & 0x2000) == 0) ltbypass = false;

                    if (!bypass)
                    {
                        if (i == 0 || (i > 1 && i < 12)) continue;
                    }
                    if (!l3bypass)
                    {
                        if (i >= 12 && i <= 15) continue;
                    }
                    if (!ltbypass)
                    {
                        if (i == 1) continue;
                    }
                    int os = o & 0xE000;
                    o = o & 0x0FFF;
                    if (o > 0x7F)
                    {

                        for (int j = 0; j < Graphics.RowCount; j++)
                        {
                            if (o.ToString("X") == Graphics.Rows[j].Cells[4].Value.ToString())
                            {
                                v = Graphics.Rows[j].Cells[3].Value.ToString();
                                short sv = short.Parse(v, System.Globalization.NumberStyles.HexNumber);

                                if (i == 0) sv = (short)(sv | (short)os);

                                long off = reader.BaseStream.Position - 2;
                                writer.Seek((int)(off), SeekOrigin.Begin);
                                writer.Write(sv);

                                break;
                            }
                        }


                    }
                }
            }
            catch (Exception e)
            {

            }
            reader.Close();
            writer.Close();

            return level;
        }

        private string GetMWLMusic(string path)
        {
            string music = "";

            int o, o2, o3, o4;
            int obj, ext;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                while (true)
                {
                    o = reader.ReadByte();
                    if (o == 0xFF)
                    {
                        reader.Close();
                        return music;
                    }

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();

                    obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                    if (obj == 0)
                    {
                        ext = o3;
                        if (ext == 0)
                        {
                            o4 = reader.ReadByte();
                        }
                        if (ext == 2)
                        {
                            o4 = reader.ReadByte();
                            o4 = reader.ReadByte();
                        }
                    }
                    else
                    {
                        switch (obj)
                        {
                            case 0x22:
                            case 0x23:
                                o4 = reader.ReadByte();
                                break;
                            case 0x26:
                                music = (o3 - 1).ToString("X");
                                reader.Close();
                                return music;
                            case 0x27:
                            case 0x29:
                                o4 = reader.ReadByte();
                                o4 &= 0xC0;

                                switch (o4)
                                {
                                    case 0x00:
                                    case 0x40:
                                        o4 = reader.ReadByte();
                                        break;
                                    case 0x80:
                                        o4 = reader.ReadByte();
                                        o4 = reader.ReadByte();
                                        break;
                                    case 0xC0:
                                        o4 = reader.ReadByte();
                                        o4 = reader.ReadByte();
                                        o4 = reader.ReadByte();
                                        break;
                                }

                                break;
                            case 0x2D:
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
            reader.Close();

            return music;
        }


        private void SetMWLMusic(string path, byte music)
        {

            int o, o2, o3, o4;
            int obj, ext;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                while (true)
                {
                    o = reader.ReadByte();
                    if (o == 0xFF)
                    {
                        reader.Close();
                        writer.Close();
                        return;
                    }

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();

                    obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                    if (obj == 0)
                    {
                        ext = o3;
                        if (ext == 0)
                        {
                            o4 = reader.ReadByte();
                        }
                        if (ext == 2)
                        {
                            o4 = reader.ReadByte();
                            o4 = reader.ReadByte();
                        }
                    }
                    else
                    {
                        switch (obj)
                        {
                            case 0x22:
                            case 0x23:
                                o4 = reader.ReadByte();
                                break;
                            case 0x26:
                                long off = reader.BaseStream.Position - 1;
                                writer.Seek((int)(off), SeekOrigin.Begin);
                                writer.Write((byte)(music+1));

                                reader.Close();
                                writer.Close();
                                return;
                            case 0x27:
                            case 0x29:
                                o4 = reader.ReadByte();
                                o4 &= 0xC0;

                                switch (o4)
                                {
                                    case 0x00:
                                    case 0x40:
                                        o4 = reader.ReadByte();
                                        break;
                                    case 0x80:
                                        o4 = reader.ReadByte();
                                        o4 = reader.ReadByte();
                                        break;
                                    case 0xC0:
                                        o4 = reader.ReadByte();
                                        o4 = reader.ReadByte();
                                        o4 = reader.ReadByte();
                                        break;
                                }

                                break;
                            case 0x2D:
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
            reader.Close();
            writer.Close();

            return;
        }


        private string GetMWLMap16(string path, ref string range)
        {

            int o, o2, o3, o4, o5, o6, o7;
            int obj, ext;
            int min, max;
            int m16, mw, mh;
            min = 0;
            max = 0;
            mw = 1;
            mh = 1;

            if (range != "" && range != "-")
            {
                min = int.Parse(range.Split('-')[0], System.Globalization.NumberStyles.HexNumber);
                max = int.Parse(range.Split('-')[1], System.Globalization.NumberStyles.HexNumber);
            }

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                while (true)
                {
                    o = reader.ReadByte();
                    if (o == 0xFF)
                    {
                        break;
                    }

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();

                    obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                    if (obj == 0)
                    {
                        ext = o3;
                        if (ext == 0)
                        {
                            o4 = reader.ReadByte();
                        }
                        if (ext == 2)
                        {
                            o4 = reader.ReadByte();
                            o4 = reader.ReadByte();
                        }
                    }
                    else
                    {
                        switch (obj)
                        {
                            case 0x22:
                            case 0x23:
                                o4 = reader.ReadByte();
                                break;
                            case 0x27:
                            case 0x29:
                                o4 = reader.ReadByte();
                                o = o4 & 0xC0;

                                o5 = reader.ReadByte();

                                m16 = (o4 & 0x3F) * 0x100 + o5;

                                if (obj == 0x29) m16 += 0x4000;

                                switch (o)
                                {
                                    case 0x00:
                                        mw = 1;
                                        mh = 1;
                                        break;
                                    case 0x40:
                                        mw = o3 & 0x0F;
                                        mh = (o3 & 0xF0) / 16;
                                        break;
                                    case 0x80:
                                        o6 = reader.ReadByte();
                                        mw = o6 & 0x0F;
                                        mh = (o6 & 0xF0) / 16;
                                        break;
                                    case 0xC0:
                                        o6 = reader.ReadByte();
                                        o7 = reader.ReadByte();

                                        mw = o6 & 0x0F;
                                        mh = (o6 & 0xF0) / 16;
                                        break;
                                }

                                if (m16 < min || min == 0) min = m16 & 0xFF00;
                                if (((m16 + mw + mh * 0x10) | 0xFF) > max || max == 0) max = ((m16 + mw + mh * 0x10) | 0xFF);

                                range = min.ToString("X") + "-" + max.ToString("X");


                                break;
                            case 0x2D:
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }



            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 16;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                o = reader.ReadByte();
                if (o == 0)
                {
                    reader.BaseStream.Seek(12, SeekOrigin.Current);
                    while (true)
                    {
                        o = reader.ReadByte();
                        if (o == 0xFF)
                        {
                            reader.Close();
                            return range;
                        }

                        o2 = reader.ReadByte();
                        o3 = reader.ReadByte();

                        obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                        if (obj == 0)
                        {
                            ext = o3;
                            if (ext == 0)
                            {
                                o4 = reader.ReadByte();
                            }
                            if (ext == 2)
                            {
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                            }
                        }
                        else
                        {
                            switch (obj)
                            {
                                case 0x22:
                                case 0x23:
                                    o4 = reader.ReadByte();
                                    break;
                                case 0x27:
                                case 0x29:
                                    o4 = reader.ReadByte();
                                    o = o4 & 0xC0;

                                    o5 = reader.ReadByte();

                                    m16 = (o4 & 0x3F) * 0x100 + o5;

                                    if (obj == 0x29) m16 += 0x4000;

                                    switch (o)
                                    {
                                        case 0x00:
                                            mw = 1;
                                            mh = 1;
                                            break;
                                        case 0x40:
                                            mw = o3 & 0x0F;
                                            mh = (o3 & 0xF0) / 16;
                                            break;
                                        case 0x80:
                                            o6 = reader.ReadByte();
                                            mw = o6 & 0x0F;
                                            mh = (o6 & 0xF0) / 16;
                                            break;
                                        case 0xC0:
                                            o6 = reader.ReadByte();
                                            o7 = reader.ReadByte();

                                            mw = o6 & 0x0F;
                                            mh = (o6 & 0xF0) / 16;
                                            break;
                                    }

                                    if (m16 < min || min == 0) min = m16 & 0xFF00;
                                    if (((m16 + mw + mh * 0x10) | 0xFF) > max || max == 0) max = ((m16 + mw + mh * 0x10) | 0xFF);

                                    range = min.ToString("X") + "-" + max.ToString("X");


                                    break;
                                case 0x2D:
                                    o4 = reader.ReadByte();
                                    o4 = reader.ReadByte();
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }

            reader.Close();

            return range;
        }

        private void GetMWLEntrances(string path, string l, string sl, ref int se)
        {

            int o, o2, o3, o4, o5, o6, o7;
            string v;
            int size;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 * 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                size = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                size -= 8;
                
                while (true)
                {
                    if (size <= 0)
                    {
                        reader.Close();
                        return;
                    }
                    o = reader.ReadByte();
                    o = o + 256 * reader.ReadByte();

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();
                    o4 = reader.ReadByte();
                    o5 = reader.ReadByte();
                    o6 = reader.ReadByte();
                    o7 = reader.ReadByte();

                    Exits.Rows.Add(l, sl, "Secondary Entrance", "", se.ToString("X"), o.ToString("X"), "", "");
                    se++;

                    size -= 8;

                }
            }
            catch (Exception e)
            {

            }

            reader.Close();

            return;
        }


        private void SetMWLEntrances(string path, string l)
        {

            int o, o2, o3, o4, o5, o6, o7;
            string v = "";
            int size;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 * 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                size = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                size -= 8;

                while (true)
                {
                    if (size <= 0)
                    {
                        reader.Close();
                        writer.Close();
                        return;
                    }
                    o = reader.ReadByte();
                    o = o + 256 * reader.ReadByte();


                    int i;

                    for (i = 0; i < Exits.RowCount; i++)
                    {
                        if (Exits.Rows[i].Cells[0].Value.ToString() == l)
                        {
                            if (Exits.Rows[i].Cells[5].Value.ToString() == o.ToString("X"))
                            {
                                v = Exits.Rows[i].Cells[4].Value.ToString();
                                break;
                            }
                        }
                    }
                    if(i < Exits.RowCount)
                    {
                        short e = (short)int.Parse(v, System.Globalization.NumberStyles.HexNumber);

                        long off = reader.BaseStream.Position - 2;
                        writer.BaseStream.Seek((int)off, SeekOrigin.Begin);
                        writer.Write(e);

                    }
                    //Exits.Rows.Add(l, sl, "Secondary Entrance", "", se.ToString("X"), o.ToString("X"), "", "");

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();
                    o4 = reader.ReadByte();
                    o5 = reader.ReadByte();
                    o6 = reader.ReadByte();
                    o7 = reader.ReadByte();

                    size -= 8;

                }
            }
            catch (Exception e)
            {

            }

            reader.Close();
            writer.Close();
            return;
        }

        private void GetMWLExits(string path, string l, string sl)
        {

            int o, o2, o3, o4, o5, o6, o7;
            int obj, ext;
            string v;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                while (true)
                {
                    o = reader.ReadByte();
                    if (o == 0xFF)
                    {
                        break;
                    }

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();

                    obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                    if (obj == 0)
                    {
                        ext = o3;
                        if (ext == 0)
                        {
                            o4 = reader.ReadByte();

                            o3 = o2 & 0x01;
                            o3 *= 256;
                            o3 += o4;

                            if ((o2 & 0x02) == 0)
                            {
                                int i;
                                v = "";
                                for (i = 0; i < Levels.RowCount; i++)
                                {
                                    if (Levels.Rows[i].Cells[0].Value.ToString() == l)
                                    {
                                        if (Levels.Rows[i].Cells[2].Value.ToString() == o3.ToString("X"))
                                        {
                                            v = Levels.Rows[i].Cells[1].Value.ToString();
                                            break;
                                        }
                                    }
                                }

                                if (i >= Levels.RowCount)
                                {
                                    MessageBox.Show($"Level exit leads to level {o3.ToString("X")} which was not included as an mwl.");
                                }
                                Exits.Rows.Add(l, sl, "Screen Exit", o.ToString("X"), "", "", v, o3.ToString("X"));
                            }
                            else
                            {
                                int i;
                                v = "";
                                for (i = 0; i < Exits.RowCount; i++)
                                {
                                    if (Exits.Rows[i].Cells[0].Value.ToString() == l)
                                    {
                                        if (Exits.Rows[i].Cells[5].Value.ToString() == o3.ToString("X"))
                                        {
                                            v = Exits.Rows[i].Cells[4].Value.ToString();
                                            break;
                                        }
                                    }
                                }

                                if (i >= Levels.RowCount)
                                {
                                    MessageBox.Show($"Secondary exit leads to exit {o3.ToString("X")} which was not included as an mwl.");
                                }

                                Exits.Rows.Add(l, sl, "Secondary Exit", o.ToString("X"), "", "", v, o3.ToString("X"));
                            }
                        }
                        if (ext == 2)
                        {
                            o4 = reader.ReadByte();
                            o4 = reader.ReadByte();
                        }
                    }
                    else
                    {
                        switch (obj)
                        {
                            case 0x22:
                            case 0x23:
                                o4 = reader.ReadByte();
                                break;
                            case 0x27:
                            case 0x29:
                                o4 = reader.ReadByte();
                                o = o4 & 0xC0;

                                o5 = reader.ReadByte();


                                switch (o)
                                {

                                    case 0x80:
                                        o6 = reader.ReadByte();
                                        break;
                                    case 0xC0:
                                        o6 = reader.ReadByte();
                                        o7 = reader.ReadByte();
                                        break;
                                }

                                break;
                            case 0x2D:
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }

            reader.Close();

            return;
        }

        private void SetMWLExits(string path, string l, string sl)
        {

            int o, o2, o3, o4, o5, o6, o7;
            int obj, ext;
            string v;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                while (true)
                {
                    o = reader.ReadByte();
                    if (o == 0xFF)
                    {
                        break;
                    }

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();

                    obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                    if (obj == 0)
                    {
                        ext = o3;
                        if (ext == 0)
                        {
                            o4 = reader.ReadByte();

                            o3 = o2 & 0x01;
                            o3 *= 256;
                            o3 += o4;

                            if ((o2 & 0x02) == 0)
                            {
                                int i;
                                v = "";
                                for (i = 0; i < Exits.RowCount; i++)
                                {
                                    if (Exits.Rows[i].Cells[0].Value.ToString() == l && Exits.Rows[i].Cells[1].Value.ToString() == sl)
                                    {
                                        if (Exits.Rows[i].Cells[3].Value.ToString() == o.ToString("X"))
                                        {
                                            v = Exits.Rows[i].Cells[6].Value.ToString();
                                            break;
                                        }
                                    }
                                }

                                short e = short.Parse(v, System.Globalization.NumberStyles.HexNumber);

                                o2 = o2 & 0xFE;
                                o4 = e & 0x00FF;
                                e = (short)(e >> 8);
                                o2 = (short)o2 | e;

                                long off = reader.BaseStream.Position - 3;
                                writer.BaseStream.Seek((int)off, SeekOrigin.Begin);
                                writer.Write((byte)o2);
                                writer.Write((byte)0);
                                writer.Write((byte)o4);

                            }
                            else
                            {
                                int i;
                                v = "";
                                for (i = 0; i < Exits.RowCount; i++)
                                {
                                    if (Exits.Rows[i].Cells[0].Value.ToString() == l && Exits.Rows[i].Cells[1].Value.ToString() == sl)
                                    {
                                        if (Exits.Rows[i].Cells[3].Value.ToString() == o.ToString("X"))
                                        {
                                            v = Exits.Rows[i].Cells[6].Value.ToString();
                                            break;
                                        }
                                    }
                                }

                                short e = short.Parse(v, System.Globalization.NumberStyles.HexNumber);

                                o2 = o2 & 0xFE;
                                o4 = e & 0x00FF;
                                e = (short)(e >> 8);
                                o2 = (short)o2 | e;

                                long off = reader.BaseStream.Position - 3;
                                writer.BaseStream.Seek((int)off, SeekOrigin.Begin);
                                writer.Write((byte)o2);
                                writer.Write((byte)0);
                                writer.Write((byte)o4);
                            }
                        }
                        if (ext == 2)
                        {
                            o4 = reader.ReadByte();
                            o4 = reader.ReadByte();
                        }
                    }
                    else
                    {
                        switch (obj)
                        {
                            case 0x22:
                            case 0x23:
                                o4 = reader.ReadByte();
                                break;
                            case 0x27:
                            case 0x29:
                                o4 = reader.ReadByte();
                                o = o4 & 0xC0;

                                o5 = reader.ReadByte();


                                switch (o)
                                {

                                    case 0x80:
                                        o6 = reader.ReadByte();
                                        break;
                                    case 0xC0:
                                        o6 = reader.ReadByte();
                                        o7 = reader.ReadByte();
                                        break;
                                }

                                break;
                            case 0x2D:
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }

            reader.Close();
            writer.Close();

            return;
        }

        private void ChangeMWLMap16(string path, int offset)
        {

            int o, o2, o3, o4, o5, o6, o7;
            int obj, ext;
            int m16, mw, mh;
            mw = 1;
            mh = 1;

            long off;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 5 + 8;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                while (true)
                {
                    o = reader.ReadByte();
                    if (o == 0xFF)
                    {
                        break;
                    }

                    o2 = reader.ReadByte();
                    o3 = reader.ReadByte();

                    obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                    if (obj == 0)
                    {
                        ext = o3;
                        if (ext == 0)
                        {
                            o4 = reader.ReadByte();
                        }
                        if (ext == 2)
                        {
                            o4 = reader.ReadByte();
                            o4 = reader.ReadByte();
                        }
                    }
                    else
                    {
                        switch (obj)
                        {
                            case 0x22:
                            case 0x23:
                                o4 = reader.ReadByte();
                                break;
                            case 0x27:
                            case 0x29:
                                off = reader.BaseStream.Position;
                                o4 = reader.ReadByte();
                                o = o4 & 0xC0;

                                o5 = reader.ReadByte();

                                m16 = (o4) * 0x100 + o5;

                                //if (obj == 0x29) m16 += 0x4000;

                                m16 += offset;
                                writer.Seek((int)off, SeekOrigin.Begin);
                                writer.Write((byte)((m16 & 0xFF00) / 0x100));
                                writer.Write((byte)(m16 & 0x000FF));
                                switch (o)
                                {
                                    case 0x00:
                                        mw = 1;
                                        mh = 1;
                                        break;
                                    case 0x40:
                                        mw = o3 & 0x0F;
                                        mh = (o3 & 0xF0) / 16;
                                        break;
                                    case 0x80:
                                        o6 = reader.ReadByte();
                                        mw = o6 & 0x0F;
                                        mh = (o6 & 0xF0) / 16;
                                        break;
                                    case 0xC0:
                                        o6 = reader.ReadByte();
                                        o7 = reader.ReadByte();

                                        mw = o6 & 0x0F;
                                        mh = (o6 & 0xF0) / 16;
                                        break;
                                }




                                break;
                            case 0x2D:
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }



            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 16;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                o = reader.ReadByte();
                if (o == 0)
                {
                    reader.BaseStream.Seek(12, SeekOrigin.Current);
                    while (true)
                    {
                        o = reader.ReadByte();
                        if (o == 0xFF)
                        {
                            reader.Close();
                            writer.Close();
                            return;
                        }

                        o2 = reader.ReadByte();
                        o3 = reader.ReadByte();

                        obj = (o & 0x60) / 2 | (o2 & 0xF0) / 16;

                        if (obj == 0)
                        {
                            ext = o3;
                            if (ext == 0)
                            {
                                o4 = reader.ReadByte();
                            }
                            if (ext == 2)
                            {
                                o4 = reader.ReadByte();
                                o4 = reader.ReadByte();
                            }
                        }
                        else
                        {
                            switch (obj)
                            {
                                case 0x22:
                                case 0x23:
                                    o4 = reader.ReadByte();
                                    break;
                                case 0x27:
                                case 0x29:
                                    off = reader.BaseStream.Position;
                                    o4 = reader.ReadByte();
                                    o = o4 & 0xC0;

                                    o5 = reader.ReadByte();

                                    m16 = (o4) * 0x100 + o5;

                                    m16 += offset;
                                    writer.Seek((int)off, SeekOrigin.Begin);
                                    writer.Write((byte)((m16 & 0xFF00) / 0x100));
                                    writer.Write((byte)(m16 & 0x000FF));

                                    switch (o)
                                    {
                                        case 0x00:
                                            mw = 1;
                                            mh = 1;
                                            break;
                                        case 0x40:
                                            mw = o3 & 0x0F;
                                            mh = (o3 & 0xF0) / 16;
                                            break;
                                        case 0x80:
                                            o6 = reader.ReadByte();
                                            mw = o6 & 0x0F;
                                            mh = (o6 & 0xF0) / 16;
                                            break;
                                        case 0xC0:
                                            o6 = reader.ReadByte();
                                            o7 = reader.ReadByte();

                                            mw = o6 & 0x0F;
                                            mh = (o6 & 0xF0) / 16;
                                            break;
                                    }



                                    break;
                                case 0x2D:
                                    o4 = reader.ReadByte();
                                    o4 = reader.ReadByte();
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }

            reader.Close();
            writer.Close();
        }

        private string GetMWLMap16BG(string path, ref string range)
        {

            int o;
            int min, max;
            int m16;
            min = 0;
            max = 0;

            if (range != "" && range != "-")
            {
                min = int.Parse(range.Split('-')[0], System.Globalization.NumberStyles.HexNumber);
                max = int.Parse(range.Split('-')[1], System.Globalization.NumberStyles.HexNumber);
            }

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 16;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                o = reader.ReadByte();
                if (o != 0)
                {
                    reader.BaseStream.Seek(7, SeekOrigin.Current);
                    for (int i = 0; i < 1024; i++)
                    {
                        m16 = reader.ReadInt16();
                        m16 |= 0x8000;

                        if (m16 >= 0x8200)
                        {

                            if (m16 < min || min == 0) min = m16 & 0xFF00;
                            if (((m16) | 0xFF) > max || max == 0) max = ((m16) | 0xFF);

                            range = min.ToString("X") + "-" + max.ToString("X");
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }

            reader.Close();

            return range;
        }


        private void ChangeMWLMap16BG(string path, int offset)
        {

            int o;
            int m16;


            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 16;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32();
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                o = reader.ReadByte();
                if (o != 0)
                {
                    reader.BaseStream.Seek(7, SeekOrigin.Current);
                    long off = reader.BaseStream.Position;
                    writer.BaseStream.Seek((int)off, SeekOrigin.Begin);
                    for (int i = 0; i < 1024; i++)
                    {
                        m16 = reader.ReadInt16();
                        if (m16 >= 0x0200)
                        {
                            m16 += offset;
                        }
                        writer.Write((byte)((m16 & 0x00FF)));
                        writer.Write((byte)((m16 & 0xFF00) / 0x100));
                        
                    }
                }
            }
            catch (Exception e)
            {

            }

            reader.Close();
            writer.Close();
        }

        private string GetMap16(string path, ref string range, bool bg)
        {

            int o;
            int min, max;
            min = 0;
            max = 0;

            int h, start, end;

            if (range != "" && range != "-")
            {
                min = int.Parse(range.Split('-')[0], System.Globalization.NumberStyles.HexNumber);
                max = int.Parse(range.Split('-')[1], System.Globalization.NumberStyles.HexNumber);
            }

            try
            {
                BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));
                reader.BaseStream.Seek(0x1C, SeekOrigin.Begin);
                h = reader.ReadInt32();

                reader.BaseStream.Seek(0x24, SeekOrigin.Begin);
                start = reader.ReadInt32();
                start *= 16;
                if(bg)
                    start += 0x4000;

                end = start + (h - 1) * 16 + 0x0F;

                if (start < min) min = start & 0xFF00;
                if (end > max) max = end | 0x00FF;

                range = min.ToString("X") + "-" + max.ToString("X");

                reader.Close();
            }
            catch (Exception e)
            {

            }


            return range;
        }


        private void SetMap16(string path, int del)
        {

            int start;


            try
            {
                BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
                BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));


                reader.BaseStream.Seek(0x24, SeekOrigin.Begin);
                writer.BaseStream.Seek(0x24, SeekOrigin.Begin);
                start = reader.ReadInt32();

                start += (del/16);
                writer.Write(start);

                reader.Close();
                writer.Close();
            }
            catch (Exception e)
            {

            }
        }

        private void SplitMap16(string path)
        {
            String fgfile = Path.GetDirectoryName(path);
            fgfile += "\\fg.map16";
            String bgfile = Path.GetDirectoryName(path);
            bgfile += "\\bg.map16";

            uint fgs = 0;
            uint bgs = 0;

            uint fge = 0;
            uint bge = 0;

            uint fgs16 = 0;
            uint bgs16 = 0;

            uint fge16 = 0;
            uint bge16 = 0;

            uint ind = 4096;

            int h, h2;

            int to, ao, bo;

            BinaryReader reader = null;
            BinaryWriter writer = null;

            try
            {
                if (!File.Exists(path)) return;

                if (File.Exists(fgfile))
                {
                    File.Delete(fgfile);
                }
                if (File.Exists(bgfile))
                {
                    File.Delete(bgfile);
                }

                reader = new BinaryReader(File.Open(path, FileMode.Open));
                
                reader.BaseStream.Seek(0x1C, SeekOrigin.Begin);
                h = reader.ReadInt32();

                if (h != 4096)
                {
                    reader.Close();
                    return;
                }

                writer = new BinaryWriter(File.Open(fgfile, FileMode.CreateNew));

                reader.BaseStream.Seek(0x10, SeekOrigin.Begin);
                bo = reader.ReadInt32();

                reader.BaseStream.Seek(bo, SeekOrigin.Begin);
                to = reader.ReadInt32();
                h = reader.ReadInt32();
                ao = reader.ReadInt32();

                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < to / 4; i++)
                {
                    h = reader.ReadInt32();
                    writer.Write(h);
                }

                reader.BaseStream.Seek(to + 4096, SeekOrigin.Begin);

                while (ind < 262144)
                {
                    h = reader.ReadInt32();
                    h2 = reader.ReadInt32();

                    if (h == 0x10041004 && h2 == 0x10041004)
                    {
                        ind += 8;
                        continue;
                    }

                    if (fgs == 0) fgs = ind;
                    fge = ind;

                    ind += 8;
                }

                fgs16 = fgs / 8;
                fge16 = fge / 8;

                fgs16 &= 0xFFFFFF00;
                fge16 &= 0xFFFFFF00;
                fge16 += 0xFF;

                fgs = fgs16 * 8;
                fge = fge16 * 8;

                if (fge == 0)
                {
                    writer.Close();
                    File.Delete(fgfile);
                }
                else
                {
                    reader.BaseStream.Seek(to + fgs, SeekOrigin.Begin);
                    ind = fgs;
                    while (ind <= fge + 4)
                    {
                        h = reader.ReadInt32();
                        writer.Write(h);
                        ind += 4;
                    }

                    fgs /= 4;
                    fge /= 4;

                    reader.BaseStream.Seek(ao + fgs, SeekOrigin.Begin);
                    ind = fgs;
                    while (ind <= fge)
                    {
                        h = reader.ReadInt32();
                        writer.Write(h);
                        ind += 4;
                    }


                    writer.BaseStream.Seek(bo + 4, SeekOrigin.Begin);
                    writer.Write( (uint)((fge-fgs)*4+8) );

                    writer.Write((uint)(((fge - fgs) * 4 + 8) + to));

                    writer.Write((uint)((((fge - fgs) * 4 + 8))/4));

                    for(int i=0;i<12;i++)
                        writer.Write((uint)0);


                    writer.BaseStream.Seek(0x1C, SeekOrigin.Begin);
                    writer.Write((uint)((fge - fgs) * 4 + 8)/(16*8));
                    writer.Write((uint)0);

                    writer.Write((uint)fgs16 / 16);

                    for (int i = 0; i < 6; i++)
                        writer.Write((uint)0);



                    writer.Close();
                }

                fgs = 0;

                writer = new BinaryWriter(File.Open(bgfile, FileMode.CreateNew));


                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < to / 4; i++)
                {
                    h = reader.ReadInt32();
                    writer.Write(h);
                }

                reader.BaseStream.Seek(to + 262144, SeekOrigin.Begin);

                ind = 262144;

                while (ind < 262144 * 2)
                {
                    h = reader.ReadInt32();
                    h2 = reader.ReadInt32();

                    if (h == 0x10041004 && h2 == 0x10041004)
                    {
                        ind += 8;
                        continue;
                    }

                    if (fgs == 0) fgs = ind;
                    fge = ind;

                    ind += 8;
                }

                fgs16 = fgs / 8;
                fge16 = fge / 8;

                fgs16 &= 0xFFFFFF00;
                fge16 &= 0xFFFFFF00;
                fge16 += 0xFF;

                fgs = fgs16 * 8;
                fge = fge16 * 8;

                if (fge == 0)
                {
                    writer.Close();
                    File.Delete(fgfile);
                }
                else
                {
                    reader.BaseStream.Seek(to + fgs, SeekOrigin.Begin);
                    ind = fgs;
                    while (ind <= fge + 4)
                    {
                        h = reader.ReadInt32();
                        writer.Write(h);
                        ind += 4;
                    }

                    fgs /= 4;
                    fge /= 4;


                    ind = fgs;
                    while (ind <= fge)
                    {
                        writer.Write(0x01300130);
                        ind += 4;
                    }

                    writer.BaseStream.Seek(bo + 4, SeekOrigin.Begin);
                    writer.Write((uint)((fge - fgs) * 4 + 8));

                    writer.Write((uint)(((fge - fgs) * 4 + 8) + to));

                    writer.Write((uint)((((fge - fgs) * 4 + 8)) / 4));

                    for (int i = 0; i < 12; i++)
                        writer.Write((uint)0);


                    writer.BaseStream.Seek(0x1C, SeekOrigin.Begin);
                    writer.Write((uint)((fge - fgs) * 4 + 8) / (16 * 8));
                    writer.Write((uint)0);

                    writer.Write((uint)fgs16 / 16 - 0x400);

                    for (int i = 0; i < 6; i++)
                        writer.Write((uint)0);



                    writer.Close();
                }




                reader.Close();
            }
            catch (Exception e)
            {
                if(reader!=null)reader.Close();
                if(writer!=null)writer.Close();
            }

            File.Delete(path);
        }

        int ComputeLevenshteinDistance(string source, string target)
        {
            if ((source == null) || (target == null)) return 0;
            if ((source.Length == 0) || (target.Length == 0)) return 0;
            if (source == target) return source.Length;

            int sourceWordCount = source.Length;
            int targetWordCount = target.Length;

            // Step 1
            if (sourceWordCount == 0)
                return targetWordCount;

            if (targetWordCount == 0)
                return sourceWordCount;

            int[,] distance = new int[sourceWordCount + 1, targetWordCount + 1];

            // Step 2
            for (int i = 0; i <= sourceWordCount; distance[i, 0] = i++) ;
            for (int j = 0; j <= targetWordCount; distance[0, j] = j++) ;

            for (int i = 1; i <= sourceWordCount; i++)
            {
                for (int j = 1; j <= targetWordCount; j++)
                {
                    // Step 3
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    // Step 4
                    distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceWordCount, targetWordCount];
        }

        double CalculateSimilarity(string source, string target)
        {
            if ((source == null) || (target == null)) return 0.0;
            if ((source.Length == 0) || (target.Length == 0)) return 0.0;
            if (source == target) return 1.0;

            int stepsToSame = ComputeLevenshteinDistance(source, target);
            return (1.0 - ((double)stepsToSame / (double)Math.Max(source.Length, target.Length)));
        }

        string FileToString(string path)
        {
            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));
            byte[] data = reader.ReadBytes((int)reader.BaseStream.Length);
            reader.Close();
            return System.Text.Encoding.UTF8.GetString(data, 0, data.Length);
        }

        double CompareFiles(string path, string path2)
        {
            string data, data2;

            data = FileToString(path);
            data2 = FileToString(path2);

            return CalculateSimilarity(data, data2);
        }

        private void button1_Click(object sender, EventArgs e)
        {

 
            for (int i = 0; i < 4096; i++) SharedGFX[i] = 0;

            OpenFileDialog dialog = new OpenFileDialog()
            {
                Filter = "SNES ROMs (*.smc)|*.smc",
                Title = "Open SMW ROM"
            };
            dialog.Multiselect = false;

            if (dialog.ShowDialog() == DialogResult.OK)
            {

                /*int s = FindSong(dialog.FileName, "C:\\Users\\Justin\\Desktop\\CollabTest2\\Levels\\Blizzard Buffalo\\Music\\Music\\A Wish.txt");

                if (s != -1)
                {
                    MessageBox.Show("Song 'A Wish.txt' was found in ROM '" + Path.GetFileName(dialog.FileName) + "' in slot " + s.ToString("X"));
                }*/

                string dir = Path.GetDirectoryName(dialog.FileName);
                dir += "\\Levels\\";

                int r = Levels.RowCount;
                for (int i = 0; i < r; i++)
                {
                    Levels.Rows.Remove(Levels.Rows[0]);
                }
                r = Blocks.RowCount;
                for (int i = 0; i < r; i++)
                {
                    Blocks.Rows.Remove(Blocks.Rows[0]);
                }
                r = Sprites.RowCount;
                for (int i = 0; i < r; i++)
                {
                    Sprites.Rows.Remove(Sprites.Rows[0]);
                }
                r = Music.RowCount;
                for (int i = 0; i < r; i++)
                {
                    Music.Rows.Remove(Music.Rows[0]);
                }
                r = Graphics.RowCount;
                for (int i = 0; i < r; i++)
                {
                    Graphics.Rows.Remove(Graphics.Rows[0]);
                }
                r = Map16.RowCount;
                for (int i = 0; i < r; i++)
                {
                    Map16.Rows.Remove(Map16.Rows[0]);
                }
                r = Exits.RowCount;
                for (int i = 0; i < r; i++)
                {
                    Exits.Rows.Remove(Exits.Rows[0]);
                }
                //try
                // {
                string[] files = Directory.GetFiles(dir, "*.mwl");


                if (files.Length == 0)
                {
                    MessageBox.Show("No mwl files found in Levels folder.");
                    return;
                }

                string file = dir;
                file += "shared.txt";


                StreamReader text;
                string line;

                if (File.Exists(file))
                {
                    text = new StreamReader(file);
                    while ((line = text.ReadLine()) != null)
                    {
                        line = line.Split(';')[0];
                        line = Regex.Replace(line, @"\s+", " ");
                        line.Trim();
                        if (line != "")
                        {
                            int v = int.Parse(line, System.Globalization.NumberStyles.HexNumber);
                            SharedGFX[v] = 1;
                        }

                    }

                }


                int len = 0;
                int l = 1;
                int sl = 0x25;

                int bl = 0x300;
                int blbg = 0x8200;
                int del = 0;

                int spr = 0;
                int ext = 0;
                int clu = 0;
                int sho = 0xC0;
                int gen = 0xD0;
                int spmode = 0;

                int mus = 0x29;
                int gfx = 0x80;


                string[] words;
                string item;
                string item2;

                string m16 = "";
                string m16bg = "";

                foreach (string path in files)
                {
                    m16 = "";
                    file = Path.GetFileName(path);

                    string outfile = dialog.FileName;
                    outfile = Path.GetDirectoryName(outfile);
                    outfile += "\\Output\\MWLs\\" + file;

                    File.Copy(path, outfile, true);

                    string level = GetMWLLevel(path);

                    string folder = Path.GetFileNameWithoutExtension(path);

                    string sub = Path.GetDirectoryName(path);
                    sub += "\\";
                    sub += folder;


                    item = GetUberASM(sub, level);

                    GetMWLMap16(path, ref m16);
                    GetMWLMap16BG(path, ref m16bg);

                    GetMap16(sub + "\\" + folder + ".map16", ref m16, false);
                    GetMap16(sub + "\\" + folder + "B.map16", ref m16, false);
                    GetMap16(sub + "\\" + folder + "BG.map16", ref m16bg, true);
                    GetMap16(sub + "\\FG.map16", ref m16, false);
                    GetMap16(sub + "\\BG.map16", ref m16bg, true);

                    Levels.Rows.Add(l.ToString("X"), l.ToString("X"), level, file, GetMWLMusic(path), item);

                    GetMWLGraphics(path, l.ToString("X"), l.ToString("X"), ref gfx);

                    sub += "\\Sublevels";

                    if (Directory.Exists(sub))
                    {


                        string[] subfiles = Directory.GetFiles(sub, "*.mwl");

                        if (files.Length == 0)
                        {
                            MessageBox.Show("No mwl files found in Sublevels folder.");
                            return;
                        }

                        foreach (string subpath in subfiles)
                        {
                            file = Path.GetFileName(subpath);

                            level = GetMWLLevel(subpath);

                            outfile = dialog.FileName;
                            outfile = Path.GetDirectoryName(outfile);
                            outfile += "\\Output\\MWLs\\" + file;

                            File.Copy(subpath, outfile, true);


                            sub = Path.GetDirectoryName(path);
                            sub += "\\";
                            sub += folder;

                            item = GetUberASM(sub, level);

                            GetMWLMap16(subpath, ref m16);
                            GetMWLMap16BG(subpath, ref m16bg);

                            Levels.Rows.Add(l.ToString("X"), sl.ToString("X"), level, folder + "\\Sublevels\\" + file, GetMWLMusic(subpath), item);

                            GetMWLGraphics(subpath, l.ToString("X"), sl.ToString("X"), ref gfx);

                            sl++;
                            if (sl == 0x100) sl = 0x1DC;
                        }



                    }



                    sub = Path.GetDirectoryName(path);
                    sub += "\\";
                    sub += folder;
                    sub += "\\Sprites";

                    if (Directory.Exists(sub))
                    {
                        file = sub + "\\";
                        file += "list.txt";

                        spmode = 0;

                        if (File.Exists(file))
                        {
                            text = new StreamReader(file);
                            while ((line = text.ReadLine()) != null)
                            {
                                line = line.Split(';')[0];
                                line = Regex.Replace(line, @"\s+", " ");
                                line.Trim();
                                if (line != "")
                                {

                                    if (line[line.Length - 1] == ':')
                                    {
                                        line = line.ToUpper();
                                        if (line == "SPRITES:") spmode = 0;
                                        if (line == "EXTENDED:") spmode = 1;
                                        if (line == "CLUSTER:") spmode = 2;
                                        continue;
                                    }

                                    words = line.Split(' ');

                                    item = words[1];

                                    words = words[0].Split(':');

                                    if (words.Length > 1)
                                    {
                                        //handle per level
                                    }
                                    else
                                    {

                                        int r1 = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);

                                        byte e1=0, e2=0;

                                        if (spmode == 0)
                                        {
                                            if (r1 < 0xB0)
                                            {
                                                GetSpriteBytes(sub + "\\" + item, ref e1, ref e2);
                                                Sprites.Rows.Add(l.ToString("X"), "Sprites", spr.ToString("X2"), words[0], item, e1.ToString(), e2.ToString());

                                                spr++;
                                            }
                                            else if (r1 < 0xD0)
                                            {
                                                GetSpriteBytes(sub + "\\Shooters\\" + item, ref e1, ref e2);
                                                Sprites.Rows.Add(l.ToString("X"), "Shooters", sho.ToString("X2"), words[0], item, e1.ToString(), e2.ToString());

                                                sho++;
                                            }
                                            else
                                            {
                                                GetSpriteBytes(sub + "\\Generators\\" + item, ref e1, ref e2);
                                                Sprites.Rows.Add(l.ToString("X"), "Generators", gen.ToString("X2"), words[0], item, e1.ToString(), e2.ToString());

                                                gen++;

                                            }
                                        }
                                        if (spmode == 1)
                                        {
                                            GetSpriteBytes(sub + "\\Extended\\" + item, ref e1, ref e2);
                                            Sprites.Rows.Add(l.ToString("X"), "Extended", ext.ToString("X2"), words[0], item, e1.ToString(), e2.ToString());

                                            ext++;
                                        }
                                        if (spmode == 2)
                                        {
                                            GetSpriteBytes(sub + "\\Cluster\\" + item, ref e1, ref e2);
                                            Sprites.Rows.Add(l.ToString("X"), "Cluster", clu.ToString("X2"), words[0], item, e1.ToString(), e2.ToString());

                                            clu++;
                                        }
                                    }
                                }
                            }
                            text.Close();
                        }
                        else
                        {
                            MessageBox.Show("Missing list.txt in " + sub);
                            return;
                        }
                    }



                    sub = Path.GetDirectoryName(path);
                    sub += "\\";
                    sub += folder;
                    sub += "\\Music";

                    if (Directory.Exists(sub))
                    {
                        file = sub + "\\";
                        file += "Addmusic_list.txt";

                        spmode = 0;

                        if (File.Exists(file))
                        {
                            text = new StreamReader(file);
                            while ((line = text.ReadLine()) != null)
                            {
                                line = line.Split(';')[0];
                                line = Regex.Replace(line, @"\s+", " ");
                                line.Trim();
                                if (line != "")
                                {

                                    if (line[line.Length - 1] == ':')
                                    {
                                        continue;
                                    }

                                    words = line.Split(' ');

                                    item = words[1];

                                    int r1 = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);

                                    if (r1 > 0x28)
                                    {

                                        Music.Rows.Add(l.ToString("X"), mus.ToString("X2"), words[0], item);

                                        mus++;
                                    }
                                }
                            }
                            text.Close();
                        }
                        else
                        {
                            MessageBox.Show("Missing Addmusic_list.txt in " + sub);
                            return;
                        }
                    }

                    if (m16 == "")
                    {
                        m16 = "-";
                        del = 0;
                        len = 0;
                        Map16.Rows.Add(l.ToString("X"), "FG", "", "", m16.Split('-')[0], m16.Split('-')[1]);

                    }
                    else
                    {
                        del = int.Parse(m16.Split('-')[0], System.Globalization.NumberStyles.HexNumber) - bl;
                        del *= -1;
                        len = int.Parse(m16.Split('-')[1], System.Globalization.NumberStyles.HexNumber) - int.Parse(m16.Split('-')[0], System.Globalization.NumberStyles.HexNumber);
                        Map16.Rows.Add(l.ToString("X"), "FG", bl.ToString("X"), (bl + len).ToString("X"), m16.Split('-')[0], m16.Split('-')[1]);

                        bl += len + 1;
                    }




                    if (len > 0)
                    {
                        sub = Path.GetDirectoryName(path);
                        sub += "\\";
                        sub += folder;
                        sub += "\\Blocks";

                        if (Directory.Exists(sub))
                        {
                            file = sub + "\\";
                            file += "list.txt";

                            if (File.Exists(file))
                            {
                                text = new StreamReader(file);
                                while ((line = text.ReadLine()) != null)
                                {
                                    line = line.Split(';')[0];
                                    line = Regex.Replace(line, @"\s+", " ");
                                    line.Trim();
                                    if (line != "")
                                    {
                                        words = line.Split(' ');

                                        item = words[1];

                                        words = words[0].Split(':');

                                        if (words.Length > 1) item2 = words[1];
                                        else item2 = "";

                                        if (words[0].Contains("-"))
                                        {
                                            line = words[0];


                                            words = words[0].Split('-');

                                            int r1 = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);
                                            int r2 = int.Parse(words[1], System.Globalization.NumberStyles.HexNumber);


                                            Blocks.Rows.Add(l.ToString("X"), (r1 + del).ToString("X") + "-" + (r2 + del).ToString("X"), line, item2, item);
                                        }
                                        else
                                        {
                                            int r1 = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);
                                            Blocks.Rows.Add(l.ToString("X"), (r1 + del).ToString("X"), words[0], item2, item);
                                        }

                                    }
                                }
                                text.Close();
                            }
                            else
                            {
                                MessageBox.Show("Missing list.txt in " + sub);
                                return;
                            }
                        }
                    }


                    if (m16bg == "" || m16bg == "-")
                    {
                        m16bg = "-";
                        Map16.Rows.Add(l.ToString("X"), "BG", "", "", m16bg.Split('-')[0], m16bg.Split('-')[1]);

                    }
                    else
                    {
                        del = int.Parse(m16bg.Split('-')[0], System.Globalization.NumberStyles.HexNumber) - blbg;
                        del *= -1;
                        len = int.Parse(m16bg.Split('-')[1], System.Globalization.NumberStyles.HexNumber) - int.Parse(m16bg.Split('-')[0], System.Globalization.NumberStyles.HexNumber);
                        Map16.Rows.Add(l.ToString("X"), "BG", blbg.ToString("X"), (blbg + len).ToString("X"), m16bg.Split('-')[0], m16bg.Split('-')[1]);

                        blbg += len + 1;
                    }


                    l++;
                    if (l == 0x25) l = 0x101;
                }

                ROM.Text = dialog.FileName;

                int se = 0;

                for (int i = 0; i < Levels.RowCount; i++)
                {
                    string sub = Path.GetDirectoryName(ROM.Text);
                    sub += "\\Levels\\";
                    sub += Levels.Rows[i].Cells[3].Value.ToString();

                    GetMWLEntrances(sub, Levels.Rows[i].Cells[0].Value.ToString(), Levels.Rows[i].Cells[1].Value.ToString(), ref se);

                }

                for (int i = 0; i < Levels.RowCount; i++)
                {
                    string sub = Path.GetDirectoryName(ROM.Text);
                    sub += "\\Levels\\";
                    sub += Levels.Rows[i].Cells[3].Value.ToString();

                    GetMWLExits(sub, Levels.Rows[i].Cells[0].Value.ToString(), Levels.Rows[i].Cells[1].Value.ToString());

                }



                string o = Path.GetDirectoryName(ROM.Text) + "\\Output\\Collab.smc";

                File.Copy(ROM.Text, o, true);

                /*}
                catch (Exception ex)
                {
                    ROM.Text = "";
                    MessageBox.Show("Could not load levels from Levels folder.");
                    return;
                }*/

            }

        }

        private string getLevelFolder(string level)
        {

            for (int i = 0; i < Levels.RowCount; i++)
            {
                if (Levels.Rows[i].Cells[0].Value.ToString() == level)
                {

                    return Path.GetFileNameWithoutExtension(Levels.Rows[i].Cells[3].Value.ToString());

                }
            }

            return "";
        }

        private void BuildGPS_Click(object sender, EventArgs e)
        {
            string path = ROM.Text;

            if (path.Length < 1) return;

            path = Path.GetDirectoryName(path);

            string input = path + "\\Levels\\";

            path += "\\Output\\GPS\\";

            string sub = path;

            sub += "list.txt";

            StreamWriter text = new StreamWriter(sub);

            string line, temp, level;

            for (int i = 0; i < Blocks.RowCount; i++)
            {
                line = "";


                line += Blocks.Rows[i].Cells[1].Value.ToString();

                temp = Blocks.Rows[i].Cells[3].Value.ToString();
                if (temp != "") line += ":" + temp;

                line += "\t" + Blocks.Rows[i].Cells[4].Value.ToString();

                text.WriteLine(line);

                level = getLevelFolder(Blocks.Rows[i].Cells[0].Value.ToString());

                File.Copy(input + level + "\\Blocks\\" + Blocks.Rows[i].Cells[4].Value.ToString(), path + "Blocks\\" + Blocks.Rows[i].Cells[4].Value.ToString(), true);
            }
            text.Close();
            if(compiling) Output.Text+="Successfully created GPS files.\r\n";
            else MessageBox.Show("Successfully created GPS files.");
        }

        private void RunGPS_Click(object sender, EventArgs e)
        {
            if (ROM.Text.Length < 2) return;

            System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo();

            pi.FileName = Path.GetDirectoryName(ROM.Text) + "\\Output\\GPS\\gps.bat";
            pi.WorkingDirectory = Path.GetDirectoryName(ROM.Text) + "\\Output\\GPS\\";
            pi.UseShellExecute = false;
            pi.RedirectStandardOutput = true;
            pi.CreateNoWindow = true;

            Process p = System.Diagnostics.Process.Start(pi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);

            if (output.Contains("routines/\""))
            {
                output = output.Split(new string[] { "routines/\""},StringSplitOptions.None)[1];

            }
            output = output.Trim();


            if (!compiling) MessageBox.Show(output);
            else
            {
                Output.Text += "GPS: " + output + "\r\n";
            }
        }

        private void BuildPIXI_Click(object sender, EventArgs e)
        {
            string path = ROM.Text;

            if (path.Length < 1) return;

            path = Path.GetDirectoryName(path);

            string input = path + "\\Levels\\";

            path += "\\Output\\PIXI\\";

            string sub = path;

            sub += "list.txt";

            StreamWriter text = new StreamWriter(sub);

            string line, level, temp, temp2;

            bool found = false;

            for (int i = 0; i < Sprites.RowCount; i++)
            {
                if (Sprites.Rows[i].Cells[1].Value.ToString() == "Sprites" || Sprites.Rows[i].Cells[1].Value.ToString() == "Shooters" || Sprites.Rows[i].Cells[1].Value.ToString() == "Generators")
                {

                    line = "";

                    line += Sprites.Rows[i].Cells[2].Value.ToString();

                    line += "\t" + Sprites.Rows[i].Cells[4].Value.ToString();

                    text.WriteLine(line);

                    level = getLevelFolder(Sprites.Rows[i].Cells[0].Value.ToString());


                    if (Sprites.Rows[i].Cells[1].Value.ToString() == "Sprites")
                    {
                        temp = path + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + Sprites.Rows[i].Cells[4].Value.ToString();
                        File.Copy(input + level + "\\Sprites\\" + Sprites.Rows[i].Cells[4].Value.ToString(), temp, true);

                        StreamReader cfg = new StreamReader(temp);
                        cfg.ReadLine();
                        cfg.ReadLine();
                        cfg.ReadLine();
                        cfg.ReadLine();
                        temp2 = cfg.ReadLine();
                        cfg.Close();

                        temp = path + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + temp2;
                        File.Copy(input + level + "\\Sprites\\" + temp2, temp, true);

                    }
                    else
                    {
                        temp = path + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + Sprites.Rows[i].Cells[4].Value.ToString();
                        File.Copy(input + level + "\\Sprites\\" + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + Sprites.Rows[i].Cells[4].Value.ToString(), temp, true);

                        StreamReader cfg = new StreamReader(temp);
                        cfg.ReadLine();
                        cfg.ReadLine();
                        cfg.ReadLine();
                        cfg.ReadLine();
                        cfg.Close();
                        temp2 = cfg.ReadLine();

                        temp = path + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + temp2;
                        File.Copy(input + level + "\\Sprites\\" + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + temp2, temp, true);

                    }
                }
            }

            for (int i = 0; i < Sprites.RowCount; i++)
            {
                if (Sprites.Rows[i].Cells[1].Value.ToString() == "Cluster")
                {
                    if (!found)
                    {
                        text.WriteLine("CLUSTER:");
                        found = true;
                    }
                    line = "";

                    line += Sprites.Rows[i].Cells[2].Value.ToString();

                    line += "\t" + Sprites.Rows[i].Cells[4].Value.ToString();

                    text.WriteLine(line);

                    level = getLevelFolder(Sprites.Rows[i].Cells[0].Value.ToString());

                    temp = path + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + Sprites.Rows[i].Cells[4].Value.ToString();
                    File.Copy(input + level + "\\Sprites\\" + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + Sprites.Rows[i].Cells[4].Value.ToString(), temp, true);

                }
            }

            found = false;

            for (int i = 0; i < Sprites.RowCount; i++)
            {
                if (Sprites.Rows[i].Cells[1].Value.ToString() == "Extended")
                {
                    if (!found)
                    {
                        text.WriteLine("EXTENDED:");
                        found = true;
                    }
                    line = "";

                    line += Sprites.Rows[i].Cells[2].Value.ToString();

                    line += "\t" + Sprites.Rows[i].Cells[4].Value.ToString();

                    text.WriteLine(line);

                    level = getLevelFolder(Sprites.Rows[i].Cells[0].Value.ToString());

                    temp = path + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + Sprites.Rows[i].Cells[4].Value.ToString();
                    File.Copy(input + level + "\\Sprites\\" + Sprites.Rows[i].Cells[1].Value.ToString() + "\\" + Sprites.Rows[i].Cells[4].Value.ToString(), temp, true);

                }
            }

            text.Close();
            if (compiling) Output.Text += "Successfully created PIXI files.\r\n";
            else MessageBox.Show("Successfully created PIXI files.");
        }

        private void RunPIXI_Click(object sender, EventArgs e)
        {
            if (ROM.Text.Length < 2) return;

            System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo();

            pi.FileName = Path.GetDirectoryName(ROM.Text) + "\\Output\\PIXI\\pixi.bat";
            pi.WorkingDirectory = Path.GetDirectoryName(ROM.Text) + "\\Output\\PIXI\\";
            pi.UseShellExecute = false;
            pi.RedirectStandardOutput = true;
            pi.CreateNoWindow = true;

            Process p = System.Diagnostics.Process.Start(pi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);

            if (output.Contains("routines/\""))
            {
                output = output.Split(new string[] { "routines/\"" }, StringSplitOptions.None)[1];

            }
            output = output.Trim();


            if (!compiling) MessageBox.Show(output);
            else
            {
                Output.Text += "PIXI: " + output + "\r\n";
            }
        }

        private void Build_Music_Click(object sender, EventArgs e)
        {
            string path = ROM.Text;

            if (path.Length < 1) return;

            path = Path.GetDirectoryName(path);

            string input = path + "\\Levels\\";

            path += "\\Output\\Addmusick\\";

            string sub = path;

            sub += "Addmusic_list.txt";

            StreamWriter text = new StreamWriter(sub);

            string line, level, temp, temp2;

            bool found = false;


            text.WriteLine("Globals:");
            text.WriteLine("01  originals/01 Miss.txt");
            text.WriteLine("02  originals/02 Game Over.txt");
            text.WriteLine("03  originals/03 Boss Clear.txt");
            text.WriteLine("04  originals/04 Stage Clear.txt");
            text.WriteLine("05  originals/05 Starman.txt");
            text.WriteLine("06  originals/06 P-switch.txt");
            text.WriteLine("07  originals/07 Keyhole.txt");
            text.WriteLine("08  originals/08 Iris Out.txt");
            text.WriteLine("09  originals/09 Bonus End.txt");
            text.WriteLine("");
            text.WriteLine("");
            text.WriteLine("Locals:");
            text.WriteLine("0A  originals/10 Piano.txt");
            text.WriteLine("0B  originals/11 Here We Go.txt");
            text.WriteLine("0C  originals/12 Water.txt");
            text.WriteLine("0D  originals/13 Bowser.txt");
            text.WriteLine("0E  originals/14 Boss.txt");
            text.WriteLine("0F  originals/15 Cave.txt");
            text.WriteLine("10  originals/16 Ghost.txt");
            text.WriteLine("11  originals/17 Castle.txt");
            text.WriteLine("12  originals/18 Switch Palace.txt");
            text.WriteLine("13  originals/19 Welcome.txt");
            text.WriteLine("14  originals/20 Rescue Egg.txt");
            text.WriteLine("15  originals/21 Title.txt");
            text.WriteLine("16  originals/22 Valley of Bowser Appears.txt");
            text.WriteLine("17  originals/23 Overworld.txt");
            text.WriteLine("18  originals/24 Yoshi's Island.txt");
            text.WriteLine("19  originals/25 Vanilla Dome.txt");
            text.WriteLine("1A  originals/26 Star Road.txt");
            text.WriteLine("1B  originals/27 Forest of Illusion.txt");
            text.WriteLine("1C  originals/28 Valley of Bowser.txt");
            text.WriteLine("1D  originals/29 Special World.txt");
            text.WriteLine("1E  originals/30 IntroScreen.txt");
            text.WriteLine("1F  originals/31 Bowser Scene 2.txt");
            text.WriteLine("20  originals/32 Bowser Scene 3.txt");
            text.WriteLine("21  originals/33 Bowser Defeated.txt");
            text.WriteLine("22  originals/34 Bowser Interlude.txt");
            text.WriteLine("23  originals/35 Bowser Zoom In.txt");
            text.WriteLine("24  originals/36 Bowser Zoom Out.txt");
            text.WriteLine("25  originals/37 Princess Peach is Rescued.txt");
            text.WriteLine("26  originals/38 Staff Roll.txt");
            text.WriteLine("27  originals/39 The Yoshis Are Home.txt");
            text.WriteLine("28  originals/40 Cast List.txt");

            string lastlev = "";

            for (int i = 0; i < Music.RowCount; i++)
            {
                level = getLevelFolder(Music.Rows[i].Cells[0].Value.ToString());

                if (level != lastlev)
                {
                    lastlev = level;

                    CopyDirectory(input + level + "\\Music\\music", path + "music");
                    CopyDirectory(input + level + "\\Music\\samples", path + "samples");
                }

                line = "";

                line += Music.Rows[i].Cells[1].Value.ToString();

                line += "  " + Music.Rows[i].Cells[3].Value.ToString();

                text.WriteLine(line);


            }

            text.Close();
            if (compiling) Output.Text += "Successfully created Addmusick files.\r\n";
            else MessageBox.Show("Successfully created Addmusick files.");
        }

        public class Folders
        {
            public string Source { get; private set; }
            public string Target { get; private set; }

            public Folders(string source, string target)
            {
                Source = source;
                Target = target;
            }
        }

        public static void CopyDirectory(string source, string target)
        {
            var stack = new Stack<Folders>();
            stack.Push(new Folders(source, target));

            while (stack.Count > 0)
            {
                var folders = stack.Pop();
                if(!Directory.Exists(folders.Target))
                    Directory.CreateDirectory(folders.Target);
                foreach (var file in Directory.GetFiles(folders.Source, "*.*"))
                {
                    File.Copy(file, Path.Combine(folders.Target, Path.GetFileName(file)), true);
                }

                foreach (var folder in Directory.GetDirectories(folders.Source))
                {
                    stack.Push(new Folders(folder, Path.Combine(folders.Target, Path.GetFileName(folder))));
                }
            }
        }

        private void Run_Addmusick_Click(object sender, EventArgs e)
        {
            if (ROM.Text.Length < 2) return;

            System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo();

            pi.FileName = Path.GetDirectoryName(ROM.Text) + "\\Output\\Addmusick\\addmusick.bat";
            pi.WorkingDirectory = Path.GetDirectoryName(ROM.Text) + "\\Output\\Addmusick\\";
            pi.UseShellExecute = false;
            pi.RedirectStandardOutput = true;
            pi.CreateNoWindow = true;

            Process p = System.Diagnostics.Process.Start(pi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(20000);

            if (output.Contains("Error:"))
            {
                output = output.Split(new string[] { "Error:" }, StringSplitOptions.None)[1];

            }
            else output = "Success!";
            output = output.Trim();


            if (!compiling) MessageBox.Show(output);
            else
            {
                Output.Text += "Addmusick: " + output + "\r\n";
            }
        }

        private void Update_Map16_Click(object sender, EventArgs e)
        {
            string level;
            string orig, value;
            string path;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);

            string sub = path;

            path += "\\Output\\MWLs\\";



            int v, ov;

            for (int i = 0; i < Map16.RowCount; i++)
            {
                level = getLevelFolder(Map16.Rows[i].Cells[0].Value.ToString());

                value = Map16.Rows[i].Cells[2].Value.ToString();

                if (value == "") continue;

                v = int.Parse(value.Split('-')[0], System.Globalization.NumberStyles.HexNumber);

                orig = Map16.Rows[i].Cells[4].Value.ToString();

                ov = int.Parse(orig.Split('-')[0], System.Globalization.NumberStyles.HexNumber);

                //if (v == ov) continue;

                if(Map16.Rows[i].Cells[1].Value.ToString() == "FG")
                {
                    if(v!=ov)ChangeMWLMap16(path + level + ".mwl", v - ov);

                    if (File.Exists(sub + "\\Levels\\" + level + "\\" + level + ".map16"))
                    {
                        File.Copy(sub + "\\Levels\\" + level + "\\" + level + ".map16", sub + "\\Output\\map16\\" + level + ".map16", true);
                        if (v != ov) SetMap16(sub + "\\Output\\map16\\" + level + ".map16", v - ov);
                    }
                    if (File.Exists(sub + "\\Levels\\" + level + "\\" + level + "B.map16"))
                    {
                        File.Copy(sub + "\\Levels\\" + level + "\\" + level + "B.map16", sub + "\\Output\\map16\\" + level + "B.map16", true);
                        if (v != ov) SetMap16(sub + "\\Output\\map16\\" + level + "B.map16", v - ov);
                    }
                }
                if (Map16.Rows[i].Cells[1].Value.ToString() == "BG")
                {
                    if (v != ov) ChangeMWLMap16BG(path + level + ".mwl", v - ov);

                    if (File.Exists(sub + "\\Levels\\" + level + "\\" + level + "BG.map16"))
                    {
                        File.Copy(sub + "\\Levels\\" + level + "\\" + level + "BG.map16", sub + "\\Output\\map16\\" + level + "BG.map16", true);
                        if (v != ov) SetMap16(sub + "\\Output\\map16\\" + level + "BG.map16", v - ov);
                    }
                }
            }
            if (compiling) Output.Text += "Successfully updated MWL map16.\r\n";
            else MessageBox.Show("Successfully updated MWL map16.");
        }

        private void Update_MWL_Music_Click(object sender, EventArgs e)
        {
            string level;
            string orig, value;
            string path;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);
            path += "\\Output\\MWLs\\";

            int v, ov;


            for (int i = 0; i < Levels.RowCount; i++)
            {
                if (Levels.Rows[i].Cells[4].Value.ToString() != "")
                {
                    orig = Levels.Rows[i].Cells[4].Value.ToString();
                    ov = int.Parse(orig, System.Globalization.NumberStyles.HexNumber);

                    for (int j = 0; j < Music.RowCount; j++)
                    {
                        if(Music.Rows[j].Cells[0].Value.ToString() == Levels.Rows[i].Cells[0].Value.ToString())
                        {
                            if (Music.Rows[j].Cells[2].Value.ToString() == orig)
                            {
                                level = Levels.Rows[i].Cells[3].Value.ToString();
                                level = Path.GetFileName(level);

                                value = Music.Rows[j].Cells[1].Value.ToString();

                                v = int.Parse(value, System.Globalization.NumberStyles.HexNumber);

                                if (v == ov) break;

                                SetMWLMusic(path + level, (byte)v);
                                break;
                            }
                        }

                    }
                }


            }
            if (compiling) Output.Text += "Successfully updated MWL music.\r\n";
            else MessageBox.Show("Successfully updated MWL music.");
        }

        private void SetMWLSprites(string path, string level)
        {
            int o, o2, o3, o4;

            int v, ov;
            int ext;

            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Write));
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read));

            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Begin);
                o = reader.ReadInt32() + 24;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                o = reader.ReadInt32() + 9;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                while (true)
                {
                    ext = reader.ReadByte();
                    if (ext == 0xFF)
                    {
                        reader.Close();
                        writer.Close();
                        return;
                    }
                    o = reader.ReadByte();
                    ov = reader.ReadByte();

                    if ((ext & 0x08) != 0)
                    {
                        for (int i = 0; i < Sprites.RowCount; i++)
                        {
                            if (Sprites.Rows[i].Cells[0].Value.ToString() == level)
                            {
                                o = int.Parse(Sprites.Rows[i].Cells[3].Value.ToString(), System.Globalization.NumberStyles.HexNumber);

                                if (o == ov)
                                {
                                    v = int.Parse(Sprites.Rows[i].Cells[2].Value.ToString(), System.Globalization.NumberStyles.HexNumber);

                                    long off = reader.BaseStream.Position - 1;
                                    writer.Seek((int)(off), SeekOrigin.Begin);
                                    writer.Write((byte)(v));

                                    if ((ext & 0x04) != 0)
                                    {

                                        v = int.Parse(Sprites.Rows[i].Cells[6].Value.ToString());
                                    }
                                    else
                                    {

                                        v = int.Parse(Sprites.Rows[i].Cells[5].Value.ToString());
                                    }

                                    for (int j = 0; j < v; j++) o = reader.ReadByte();
                                    break;
                                }
                            }

                        }

                    }



                }
            }
            catch (Exception e)
            {

            }
            reader.Close();
            writer.Close();
        }

        private void Update_MWL_Sprites_Click(object sender, EventArgs e)
        {
            string level;
            string orig, value;
            string path;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);
            path += "\\Output\\MWLs\\";

            int v, ov;


            for (int i = 0; i < Levels.RowCount; i++)
            {
                if (Levels.Rows[i].Cells[4].Value.ToString() != "")
                {
                    orig = Levels.Rows[i].Cells[4].Value.ToString();
                    ov = int.Parse(orig, System.Globalization.NumberStyles.HexNumber);

                    for (int j = 0; j < Sprites.RowCount; j++)
                    {
                        if (Sprites.Rows[j].Cells[0].Value.ToString() == Levels.Rows[i].Cells[0].Value.ToString())
                        {
                            level = Levels.Rows[i].Cells[3].Value.ToString();
                            level = Path.GetFileName(level);

                            value = Music.Rows[j].Cells[1].Value.ToString();

                            v = int.Parse(value, System.Globalization.NumberStyles.HexNumber);

                            if (v == ov) break;

                            SetMWLSprites(path + level, Sprites.Rows[j].Cells[0].Value.ToString());
                            break;
                        }

                    }
                }


            }
            if (compiling) Output.Text += "Successfully updated MWL sprites.\r\n";
            else MessageBox.Show("Successfully updated MWL sprites.");

        }

        private void BuildUberASM_Click(object sender, EventArgs e)
        {
            string path = ROM.Text;

            if (path.Length < 1) return;

            path = Path.GetDirectoryName(path);

            string input = path + "\\Levels\\";

            path += "\\Output\\UberASM\\";

            string sub = path;

            sub += "list.txt";

            StreamWriter text = new StreamWriter(sub);

            string line, temp, level;

            text.WriteLine("verbose: on");
            text.WriteLine("");
            text.WriteLine("; Level list. Valid values: 000-1FF.");
            text.WriteLine("level:");

            for (int i = 0; i < Levels.RowCount; i++)
            {
                if (Levels.Rows[i].Cells[5].Value.ToString().Length > 1)
                {
                    line = "";


                    line += Levels.Rows[i].Cells[1].Value.ToString();
                    line += "\t\t";

                    line += Levels.Rows[i].Cells[5].Value.ToString();

                    text.WriteLine(line);

                    level = getLevelFolder(Levels.Rows[i].Cells[0].Value.ToString());

                    File.Copy(input + level + "\\UberASM\\" + Levels.Rows[i].Cells[5].Value.ToString(), path + "level\\" + Levels.Rows[i].Cells[5].Value.ToString(), true);

                    if(Directory.Exists(input + level + "\\UberASM\\library"))
                    {
                        CopyDirectory(input + level + "\\UberASM\\library", path + "library");
                    }
                }
            }

            text.WriteLine("");
            text.WriteLine("overworld:");
            text.WriteLine("");
            text.WriteLine("gamemode:");
            text.WriteLine("");

            text.WriteLine("global:		other/global_code.asm	; global code.");
            text.WriteLine("statusbar:	other/status_code.asm	; status bar code.");
            text.WriteLine("macrolib:	other/macro_library.asm	; macro library.");
            text.WriteLine("sprite:		$7FAC80			; 38 (SNES) or 68 (SA-1) bytes of free RAM.");
            text.WriteLine("rom:		..\\Collab.smc			; ROM file to use.");


            text.Close();
            if (compiling) Output.Text += "Successfully created UberASM files.\r\n";
            else MessageBox.Show("Successfully created UberASM files.");
        }

        private void RunUberASM_Click(object sender, EventArgs e)
        {
            if (ROM.Text.Length < 2) return;

            System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo();

            pi.FileName = Path.GetDirectoryName(ROM.Text) + "\\Output\\UberASM\\UberASMTool.exe";
            pi.WorkingDirectory = Path.GetDirectoryName(ROM.Text) + "\\Output\\UberASM\\";
            pi.UseShellExecute = false;
            pi.RedirectStandardOutput = true;
            pi.RedirectStandardInput = true;
            pi.CreateNoWindow = true;

            Process p = System.Diagnostics.Process.Start(pi);
            string output = p.StandardOutput.ReadToEnd();
            p.StandardInput.WriteLine("");
            p.WaitForExit(5000);

            if (output.Contains("Error "))
            {
                output = "Error " + output.Split(new string[] { "Error " }, StringSplitOptions.None)[1];

            }
            else output = "Codes inserted successfully.";
            output = output.Trim();


            if (!compiling) MessageBox.Show(output);
            else
            {
                Output.Text += "UberASMTool: " + output + "\r\n";
            }
        }

        private void Update_Exits_Click(object sender, EventArgs e)
        {
            string level;
            string path;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);
            path += "\\Output\\MWLs\\";

            int v, ov;


            for (int i = 0; i < Levels.RowCount; i++)
            {

                level = Levels.Rows[i].Cells[3].Value.ToString();
                level = Path.GetFileName(level);


                SetMWLEntrances(path + level, Levels.Rows[i].Cells[0].Value.ToString());
                SetMWLExits(path + level, Levels.Rows[i].Cells[0].Value.ToString(), Levels.Rows[i].Cells[1].Value.ToString());
            }
            if (compiling) Output.Text += "Successfully updated MWL exits.\r\n";
            else MessageBox.Show("Successfully updated MWL exits.");
        }

        private void BuildGFX_Click(object sender, EventArgs e)
        {
            string path = ROM.Text;

            if (path.Length < 1) return;

            path = Path.GetDirectoryName(path);

            string input = path + "\\Levels\\";

            path += "\\Output\\ExGraphics\\";

            string line, temp, level;

            for (int i = 0; i < Graphics.RowCount; i++)
            {
                level = getLevelFolder(Graphics.Rows[i].Cells[0].Value.ToString());

                File.Copy(input + level + "\\ExGraphics\\ExGFX" + Graphics.Rows[i].Cells[4].Value.ToString() + ".bin", path + "ExGFX" + Graphics.Rows[i].Cells[3].Value.ToString() + ".bin", true);
            }
            if (compiling) Output.Text += "Successfully created GFX files.\r\n";
            else MessageBox.Show("Successfully created GFX files.");
        }

        private void Update_GFX_Click(object sender, EventArgs e)
        {
            string level;
            string path;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);
            path += "\\Output\\MWLs\\";


            for (int i = 0; i < Levels.RowCount; i++)
            {

                level = Levels.Rows[i].Cells[3].Value.ToString();
                level = Path.GetFileName(level);

                SetMWLGraphics(path + level, Levels.Rows[i].Cells[0].Value.ToString(), Levels.Rows[i].Cells[1].Value.ToString());
            }
            if (compiling) Output.Text += "Successfully updated MWL graphics.\r\n";
            else MessageBox.Show("Successfully updated MWL graphics.");
        }

        private void Insert_GFX_Click(object sender, EventArgs e)
        {
            string path;
            string rom;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);
            rom = path;
            rom += "\\Output\\Collab.smc";

            string cmd = "\"";
            cmd += path;
            cmd += "\\Lunar Magic.exe\"";

            string arg ="-ImportExGFX \"";
            arg += rom;
            arg += "\"";

            ProcessStartInfo pInfo = new ProcessStartInfo();

            pInfo.FileName = cmd;
            pInfo.Arguments = arg;
            Process p = Process.Start(pInfo);
            p.WaitForInputIdle();
            p.WaitForExit(5000);
            if (p.HasExited == false)
                if (p.Responding)
                    p.CloseMainWindow();
                else
                    p.Kill();

            if (compiling) Output.Text += "Successfully inserted graphics.\r\n";
            else MessageBox.Show("Successfully inserted graphics.");
        }

        private void Insert_MWLs_Click(object sender, EventArgs e)
        {
            string path;
            string level;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);

            string rom = path;
            rom += "\\Output\\Collab.smc";

            path += "\\";

            string file;

            for (int i = 0; i < Levels.RowCount; i++)
            {
                level = Levels.Rows[i].Cells[3].Value.ToString();
                level = Path.GetFileName(level);

                file = path;
                file += "Output\\MWLs\\";
                file += level;

                string cmd = "\"";
                cmd += path;
                cmd += "Lunar Magic.exe\"";

                string arg = "-ImportLevel \"";
                arg += rom;
                arg += "\" \"";

                arg += file;
                arg += "\" ";
                arg += Levels.Rows[i].Cells[1].Value.ToString();

                ProcessStartInfo pInfo = new ProcessStartInfo();

                pInfo.FileName = cmd;
                pInfo.Arguments = arg;
                Process p = Process.Start(pInfo);
                p.WaitForInputIdle();
                p.WaitForExit(5000);
                if (p.HasExited == false)
                    if (p.Responding)
                        p.CloseMainWindow();
                    else
                        p.Kill();

            }
            if (compiling) Output.Text += "Successfully inserted levels.\r\n";
            else MessageBox.Show("Successfully inserted levels.");
        }

        private void Insert_Map16_Click(object sender, EventArgs e)
        {
            string path;
            string level;

            path = ROM.Text;
            path = Path.GetDirectoryName(path);

            string rom = path;
            rom += "\\Output\\Collab.smc";

            path += "\\";

            string[] files = Directory.GetFiles(path + "Output\\map16", "*.map16");

            foreach (string file in files)
            {
                string cmd = "\"";
                cmd += path;
                cmd += "Lunar Magic.exe\"";

                string arg = "-ImportMap16 \"";
                arg += rom;
                arg += "\" \"";

                arg += file;
                arg += "\" 001";

                ProcessStartInfo pInfo = new ProcessStartInfo();

                pInfo.FileName = cmd;
                pInfo.Arguments = arg;
                Process p = Process.Start(pInfo);
                p.WaitForInputIdle();
                p.WaitForExit(5000);
                if (p.HasExited == false)
                    if (p.Responding)
                        p.CloseMainWindow();
                    else
                        p.Kill();

            }
            if (compiling) Output.Text += "Successfully inserted map16.\r\n";
            else MessageBox.Show("Successfully inserted map16.");
        }

        private void BuildCollab_Click(object sender, EventArgs e)
        {
            if (ROM.Text.Length < 1) return;
            compiling = true;
            Output.Text = "";

            BuildGPS_Click(null, null);
            this.Update();
            Application.DoEvents();
            RunGPS_Click(null, null);
            this.Update();
            Application.DoEvents();

            BuildPIXI_Click(null, null);
            this.Update();
            Application.DoEvents();
            RunPIXI_Click(null, null);
            this.Update();
            Application.DoEvents();
            Update_MWL_Sprites_Click(null, null);
            this.Update();
            Application.DoEvents();

            Build_Music_Click(null, null);
            this.Update();
            Application.DoEvents();
            Run_Addmusick_Click(null, null);
            this.Update();
            Application.DoEvents();
            Update_MWL_Music_Click(null, null);
            this.Update();
            Application.DoEvents();

            BuildGFX_Click(null, null);
            this.Update();
            Application.DoEvents();
            Insert_GFX_Click(null, null);
            this.Update();
            Application.DoEvents();
            Update_GFX_Click(null, null);
            this.Update();
            Application.DoEvents();

            Update_Map16_Click(null, null);
            this.Update();
            Application.DoEvents();
            Insert_Map16_Click(null, null);
            this.Update();
            Application.DoEvents();

            Update_Exits_Click(null, null);
            this.Update();
            Application.DoEvents();

            BuildUberASM_Click(null, null);
            this.Update();
            Application.DoEvents();
            RunUberASM_Click(null, null);
            this.Update();
            Application.DoEvents();
            Insert_MWLs_Click(null, null);

            compiling = false;
        }

        private void Mass_Unzip_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new System.Windows.Forms.FolderBrowserDialog();
            String path;
            DialogResult result = folder.ShowDialog();
            if (result == DialogResult.OK)
            {
                Output.Text = "";
                path = folder.SelectedPath;

                Cursor.Current = Cursors.WaitCursor;
                Application.DoEvents();

                foreach (var file in Directory.GetFiles(path, "*.*"))
                {
                    if (file.EndsWith(".zip"))
                    {
                        try
                        {

                            Output.Text += "Extracting " + file + "\r\n";
                            this.Update();
                            Application.DoEvents();

                            ZipArchive archive = ZipFile.OpenRead(file);
                            if (Zip_Dirs.Checked)
                            {
                                String sub = Path.GetFileNameWithoutExtension(file);
                                if (!Directory.Exists(path + "\\" + sub))
                                    Directory.CreateDirectory(path + "\\" + sub);
                                archive.ExtractToDirectory(path + "\\" + sub);
                            }
                            else
                            {
                                archive.ExtractToDirectory(path);
                            }



                            archive.Dispose();
                            if (Delete_Zips.Checked)
                            {
                                File.Delete(file);
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
                Cursor.Current = Cursors.Default;
                MessageBox.Show("Files Extracted");
            }
        }

        private void Rename_Patches_Click(object sender, EventArgs e)
        {

            FolderBrowserDialog folder = new System.Windows.Forms.FolderBrowserDialog();
            String path;
            DialogResult result = folder.ShowDialog();
            if (result == DialogResult.OK)
            {
                Output.Text = "";

                Cursor.Current = Cursors.WaitCursor;
                Application.DoEvents();

                path = folder.SelectedPath;

                String wd = Directory.GetCurrentDirectory();

                RenamePatches(wd, path, path);

                Cursor.Current = Cursors.Default;
                MessageBox.Show("Patches Renamed");
            }
        }

        private void RenamePatches(String wd, String root, String sub)
        {

            foreach (var file in Directory.GetFiles(sub, "*.*"))
            {
                if (file.EndsWith(".bps") || file.EndsWith(".ips"))
                {
                    try
                    {
                        String filename = Path.GetFileName(file);
                        String dir = Path.GetDirectoryName(sub + "\\");
                        dir = dir.Split('\\').Last();
                        String newname = dir + Path.GetExtension(filename);

                        if (Move_Patches.Checked && root != sub)
                        {
                            Output.Text += "Renaming " + file + " to " + root + "\\" + newname + "\r\n";
                            this.Update();
                            Application.DoEvents();
                            File.Move(file, root + "\\" + newname);
                        }
                        else
                        {
                            Output.Text += "Renaming " + file + " to " + sub + "\\" + newname + "\r\n";
                            this.Update();
                            Application.DoEvents();
                            File.Move(file, sub + "\\" + newname);
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            foreach (var folder in Directory.GetDirectories(sub))
            {
                RenamePatches(wd, root, folder);
            }
        }

        private void Apply_Patches_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new System.Windows.Forms.FolderBrowserDialog();
            String path;
            DialogResult result = folder.ShowDialog();
            if (result == DialogResult.OK)
            {
                Output.Text = "";

                Cursor.Current = Cursors.WaitCursor;
                Application.DoEvents();

                path = folder.SelectedPath;

                String wd = Directory.GetCurrentDirectory();

                if(!File.Exists(wd+"\\flips.exe"))
                {
                    MessageBox.Show("FLIPS.exe missing from Collab Builder directory.");
                    return;
                }

                if (!File.Exists(wd + "\\SMW.smc"))
                {
                    MessageBox.Show("SMW.smc missing from Collab Builder directory.");
                    return;
                }

                ApplyPatches(wd, path, path);

                Cursor.Current = Cursors.Default;
                MessageBox.Show("Patches Applied");
            }
        }

        private void ApplyPatches(String wd, String root, String sub)
        {

            foreach (var file in Directory.GetFiles(sub, "*.*"))
            {
                if (file.EndsWith(".bps") || file.EndsWith(".ips"))
                {
                    try
                    {

                        Output.Text += "Patching " + file + "\r\n";

                        System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo();

                        String rom = file.Replace(".bps", ".smc");
                        rom = rom.Replace(".ips", ".smc");

                        pi.FileName = wd + "\\flips.exe";
                        pi.Arguments = "--apply \"" + file + "\" \"" + wd + "\\SMW.smc\" \"" + rom + "\"";
                        pi.WorkingDirectory = sub;
                        pi.UseShellExecute = false;
                        pi.RedirectStandardOutput = true;
                        pi.CreateNoWindow = true;

                        Process p = System.Diagnostics.Process.Start(pi);
                        p.WaitForExit(5000);

                        if (Delete_Patches.Checked)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            foreach (var folder in Directory.GetDirectories(sub))
            {
                ApplyPatches(wd, root, folder);
            }
        }

        private void Extract_Data_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new System.Windows.Forms.FolderBrowserDialog();
            String path;
            DialogResult result = folder.ShowDialog();
            if (result == DialogResult.OK)
            {
                Output.Text = "";

                Cursor.Current = Cursors.WaitCursor;
                Application.DoEvents();

                path = folder.SelectedPath;

                String wd = Directory.GetCurrentDirectory();
                String file2;
                String sub, cmd, arg;

                foreach (var file in Directory.GetFiles(path, "*.*"))
                {
                    if (file.EndsWith(".smc"))
                    {
                        try
                        {

                            Output.Text += "Extracting from " + file + "\r\n";

                            System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo();

                            sub = Path.GetFileNameWithoutExtension(file);

                            file2 = path + "\\" + sub;
                            if (!Directory.Exists(file2))
                            {
                                Directory.CreateDirectory(file2);
                            }

                            file2 += "\\" + Path.GetFileName(file);


                            File.Copy(file, file2, true);


                            if (ExGraphics.Checked)
                            {
                                cmd = "\"";
                                cmd += wd;
                                cmd += "\\Lunar Magic.exe\"";

                                arg = "-ExportExGFX \"";
                                arg += file2;
                                arg += "\"";

                                ProcessStartInfo pInfo = new ProcessStartInfo();

                                pInfo.FileName = cmd;
                                pInfo.Arguments = arg;
                                Process p = Process.Start(pInfo);
                                p.WaitForInputIdle();
                                p.WaitForExit(5000);
                                if (p.HasExited == false)
                                    if (p.Responding)
                                        p.CloseMainWindow();
                                    else
                                        p.Kill();
                            }

                            if (Map_16.Checked)
                            {
                                cmd = "\"";
                                cmd += wd;
                                cmd += "\\Lunar Magic.exe\"";

                                arg = "-ExportAllMap16 \"";
                                arg += file2;
                                arg += "\" \"tiles.map16\"";

                                ProcessStartInfo pInfo = new ProcessStartInfo();

                                pInfo.FileName = cmd;
                                pInfo.Arguments = arg;
                                Process p = Process.Start(pInfo);
                                p.WaitForInputIdle();
                                p.WaitForExit(5000);
                                if (p.HasExited == false)
                                    if (p.Responding)
                                        p.CloseMainWindow();
                                    else
                                        p.Kill();

                                arg = Path.GetDirectoryName(file2);
                                arg += "\\tiles.map16";
                                SplitMap16(arg);
                            }

                            if (MWLs.Checked)
                            {
                                cmd = "\"";
                                cmd += wd;
                                cmd += "\\Lunar Magic.exe\"";

                                arg = "-ExportMultLevels \"";
                                arg += file2;
                                arg += "\" \"\"";

                                ProcessStartInfo pInfo = new ProcessStartInfo();

                                pInfo.FileName = cmd;
                                pInfo.Arguments = arg;
                                Process p = Process.Start(pInfo);
                                p.WaitForInputIdle();
                                p.WaitForExit(5000);
                                if (p.HasExited == false)
                                    if (p.Responding)
                                        p.CloseMainWindow();
                                    else
                                        p.Kill();
                            }


                            File.Delete(file2);
                            if (Delete_Roms.Checked)
                            {
                                File.Delete(file);
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }


                Cursor.Current = Cursors.Default;
                MessageBox.Show("Data Extracted");
            }
        }

        private void Organize_Folders_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new System.Windows.Forms.FolderBrowserDialog();
            String path;
            DialogResult result = folder.ShowDialog();
            if (result == DialogResult.OK)
            {
                Output.Text = "";

                Cursor.Current = Cursors.WaitCursor;
                Application.DoEvents();

                path = folder.SelectedPath;

                String wd = Directory.GetCurrentDirectory();
                String file2;
                String sub, cmd, arg;

                foreach (var dir in Directory.GetDirectories(path))
                {
                    if (Directory.Exists(dir + "\\Music"))
                    {
                        if (Directory.Exists(dir + "\\Music\\Music"))
                        {

                        }
                        else
                        {
                            Directory.CreateDirectory(dir + "\\_Music");
                            Directory.Move(dir + "\\Music", dir + "\\_Music\\Music");
                            Directory.Move(dir + "\\_Music", dir + "\\Music");
                        }
                    }
                    if (Directory.Exists(dir + "\\Samples"))
                    {
                        Directory.Move(dir + "\\Samples", dir + "\\Music\\Samples");
                    }

                    if (Remove_Intro.Checked)
                    {
                        if (File.Exists(dir + "\\C7.mwl"))
                            File.Delete(dir + "\\C7.mwl");
                        if (File.Exists(dir + "\\C5.mwl"))
                            File.Delete(dir + "\\C5.mwl");
                        if (File.Exists(dir + "\\0C7.mwl"))
                            File.Delete(dir + "\\0C7.mwl");
                        if (File.Exists(dir + "\\0C5.mwl"))
                            File.Delete(dir + "\\0C5.mwl");
                        if (File.Exists(dir + "\\1C7.mwl"))
                            File.Delete(dir + "\\1C7.mwl");
                        if (File.Exists(dir + "\\1C5.mwl"))
                            File.Delete(dir + "\\1C5.mwl");
                        if (File.Exists(dir + "\\ C7.mwl"))
                            File.Delete(dir + "\\ C7.mwl");
                        if (File.Exists(dir + "\\ C5.mwl"))
                            File.Delete(dir + "\\ C5.mwl");
                        if (File.Exists(dir + "\\ 0C7.mwl"))
                            File.Delete(dir + "\\ 0C7.mwl");
                        if (File.Exists(dir + "\\ 0C5.mwl"))
                            File.Delete(dir + "\\ 0C5.mwl");
                        if (File.Exists(dir + "\\ 1C7.mwl"))
                            File.Delete(dir + "\\ 1C7.mwl");
                        if (File.Exists(dir + "\\ 1C5.mwl"))
                            File.Delete(dir + "\\ 1C5.mwl");
                    }
                    if (Remove_Yoshi.Checked)
                    {
                        if (File.Exists(dir + "\\104.mwl"))
                            File.Delete(dir + "\\104.mwl");
                        if (File.Exists(dir + "\\ 104.mwl"))
                            File.Delete(dir + "\\ 104.mwl");
                    }

                    string dest, file;
                    var list = Directory.GetFiles(dir, "*.mwl");

                    for (int i = 1; i < list.Count(); i++)
                    {
                        double sim = CompareFiles(list[i], "Test.mwl");
                        if (sim > 0.97)
                        {
                            File.Delete(list[i]);
                        }
                    }

                    list = Directory.GetFiles(dir, "*.mwl");
                    if (list.Count() > 1)
                    {
                        if (!Directory.Exists(dir + "\\Sublevels"))
                        {
                            Directory.CreateDirectory(dir + "\\Sublevels");
                        }


                        for (int i = 1; i < list.Count(); i++)
                        {
                            dest = Path.GetDirectoryName(list[i]);
                            file = Path.GetFileName(list[i]).Trim();

                            if (File.Exists(dest + "\\Sublevels\\" + file))
                                File.Delete(dest + "\\Sublevels\\" + file);

                            File.Move(list[i], dest + "\\Sublevels\\" + file);
                        }
                    }
                    if (list.Count() > 0)
                    {
                        sub = Path.GetFileName(dir);
                        dest = Path.GetDirectoryName(list[0]);
                        dest = Directory.GetParent(dest).FullName;
                        file = sub + ".mwl";

                        if (File.Exists(dest + "\\" + file))
                            File.Delete(dest + "\\" + file);

                        File.Move(list[0], dest + "\\" + file);
                    }
                }
            }

            MessageBox.Show("Data Organized");
        }


        bool HasHeader(string path)
        {
            if (!File.Exists(path)) return false;

            long size = new System.IO.FileInfo(path).Length;

            if ((size & 0x000200) != 0) return true;

            return false;
        }

        bool IsHirom(string path)
        {
            if (!File.Exists(path)) return false;
            BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

            try
            {
                int o = 0x7FD5;
                if (HasHeader(path)) o += 0x200;
                reader.BaseStream.Seek(o, SeekOrigin.Begin);
                byte v = reader.ReadByte();
                reader.Close();
                if ((v & 1) == 1) return true;
                return false;
            }
            catch(Exception e)
            {
                reader.Close();
                return false;
            }
        }

        uint SNESToPC(string path, uint addr)
        {
            return SNESToPC(path, addr & 0x0000FF, (addr & 0x00FF00)/0x100, (addr & 0xFF0000) / 0x10000);
        }

        uint SNESToPC(string path, uint addrlo, uint addrhi, uint bank)
        {

            bool header = HasHeader(path);

            if (IsHirom(path))
            {
                uint addr = (addrlo) + (addrhi * 0x100) + (bank * 0x10000);
                if ((addr & 0x400000) != 0)
                {
                    addr &= 0x3FFFFF;
                }
                else
                {
                    addr = ((addr & 0x8000)!=0) ? addr & 0x3FFFFF : 0;
                }
                return addr;
            }
            else
            {
                bank &= 0x7F;
                return (uint)((addrlo & 0xFF) + (0x100 * (addrhi & 0xFF)) + (0x8000 * bank) - ((header) ? 0 : 0x200) - 0x7E00);
            }
        }

        uint SNESToPC(bool header, bool hirom, uint addr)
        {
            return SNESToPC(header, hirom, addr & 0x0000FF, (addr & 0x00FF00) / 0x100, (addr & 0xFF0000) / 0x10000);
        }

        uint SNESToPC(bool header, bool hirom, uint addrlo, uint addrhi, uint bank)
        {

            if (hirom)
            {
                uint addr = (addrlo) + (addrhi * 0x100) + (bank * 0x10000);
                if ((addr & 0x400000) != 0)
                {
                    addr &= 0x3FFFFF;
                }
                else
                {
                    addr = ((addr & 0x8000) != 0) ? addr & 0x3FFFFF : 0;
                }
                return addr;
            }
            else
            {
                bank &= 0x7F;
                return (uint)((addrlo & 0xFF) + (0x100 * (addrhi & 0xFF)) + (0x8000 * bank) - ((header) ? 0 : 0x200) - 0x7E00);
            }
        }


        int FindSong(string rom, string song)
        {
            String wd = Directory.GetCurrentDirectory();

            if (!Directory.Exists(wd + "\\AddMusick"))
            {
                MessageBox.Show("AddMusick folder missing from Collab Builder directory.");
                return -1;
            }

            if (!File.Exists(rom)) return -1;
            if (!File.Exists(song)) return -1;

            int slot = 0;
            string file = Path.GetFileName(song);
            string sub = Directory.GetParent(song).FullName;
            sub = Directory.GetParent(sub).FullName;

            try
            {
                if (Directory.Exists(wd + "\\AddMusick\\music"))
                    Directory.Delete(wd + "\\AddMusick\\music", true);
                if (Directory.Exists(wd + "\\AddMusick\\samples"))
                    Directory.Delete(wd + "\\AddMusick\\samples", true);

                CopyDirectory(wd + "\\AddMusick\\musicbase", wd + "\\AddMusick\\music");
                CopyDirectory(wd + "\\AddMusick\\samplesbase", wd + "\\AddMusick\\samples");

                File.Copy(song, wd + "\\AddMusick\\music\\" + file);

                if (Directory.Exists(sub + "\\samples"))
                    CopyDirectory(sub + "\\samples", wd + "\\AddMusick\\samples");

                foreach (var sample in Directory.GetFiles(sub, "*.brr"))
                {
                    File.Copy(sample, wd + "\\AddMusick\\samples\\" + Path.GetFileName(sample));
                }

                if (Directory.Exists(sub + "\\samples"))
                {
                    CopyDirectory(sub + "\\samples", wd + "\\AddMusick\\samples\\");
                }
                if (Directory.Exists(sub + "\\brr"))
                {
                    CopyDirectory(sub + "\\brr", wd + "\\AddMusick\\samples\\");
                }
                if (Directory.Exists(sub + "\\brrs"))
                {
                    CopyDirectory(sub + "\\brrs", wd + "\\AddMusick\\samples\\");
                }

                if (File.Exists(wd + "\\AddMusick\\Addmusic_list.txt"))
                    File.Delete(wd + "\\AddMusick\\Addmusic_list.txt");

                if (File.Exists(wd + "\\AddMusick\\Addmusic_sample groups.txt"))
                    File.Delete(wd + "\\AddMusick\\Addmusic_sample groups.txt");

                StreamWriter writer = new StreamWriter(wd + "\\AddMusick\\Addmusic_list.txt");
                writer.WriteLine("Locals:");
                writer.WriteLine("0A  " + file);
                writer.Close();

                foreach (var tfile in Directory.GetFiles(sub, "*.txt"))
                {
                    bool go = false;
                    if (tfile.ToLower().Contains("samplegroup")) go = true;
                    if (tfile.ToLower().Contains("sample group")) go = true;
                    if (tfile.ToLower().Contains("samples group")) go = true;
                    if (tfile.ToLower().Contains("sample_group")) go = true;

                    StreamReader text = new StreamReader(tfile);
                    string line = null;

                    if (!go)
                        if (tfile.ToLower().Contains("read me") || tfile.ToLower().Contains("read_me") || tfile.ToLower().Contains("readme"))
                        {
                            line = text.ReadLine();
                            if (line == null)
                            {
                                text.Close();
                                continue;
                            }

                            line = line.Trim();
                            if (line.StartsWith("#")) go = true;
                        }

                    if (!go)
                    {
                        text.Close();
                        continue;
                    }
                    StreamWriter twriter = File.AppendText(wd + "\\AddMusick\\Addmusic_sample groups.txt");


                    twriter.WriteLine("");
                    if (line != null && !line.StartsWith(";")) twriter.WriteLine(line);

                    while ((line = text.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.StartsWith(";")) continue;
                        twriter.WriteLine(line);
                    }
                    text.Close();
                    twriter.Close();
                }

                if (File.Exists(wd + "\\AddMusick\\Dummy.smc"))
                    File.Delete(wd + "\\AddMusick\\Dummy.smc");

                File.Copy(wd + "\\AddMusick\\Base.smc", wd + "\\AddMusick\\Dummy.smc");

                System.Diagnostics.ProcessStartInfo pi = new System.Diagnostics.ProcessStartInfo();

                pi.FileName = wd + "\\AddMusick\\_Add.bat";
                pi.WorkingDirectory = wd + "\\AddMusick\\";
                pi.UseShellExecute = false;
                pi.RedirectStandardOutput = true;
                pi.CreateNoWindow = true;

                Process p = System.Diagnostics.Process.Start(pi);
                //string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(20000);
                if (p.HasExited == false)
                {
                    p.Kill();
                    return -1;
                }

                BinaryReader creader = new BinaryReader(File.Open(wd + "\\AddMusick\\Dummy.smc", FileMode.Open, FileAccess.Read, FileShare.Read));
                uint co = 0, cdato = 0;
                co = SNESToPC(HasHeader(wd + "\\AddMusick\\Dummy.smc"), false, 0xE8000);

                creader.BaseStream.Seek(co + 8, SeekOrigin.Begin);
                co = creader.ReadUInt32();
                co &= 0x00FFFFFF;

                co = SNESToPC(HasHeader(wd + "\\AddMusick\\Dummy.smc"), false, co);
                creader.BaseStream.Seek(co+30, SeekOrigin.Begin);
                cdato = creader.ReadUInt32();
                cdato &= 0x00FFFFFF;
                cdato = SNESToPC(HasHeader(wd + "\\AddMusick\\Dummy.smc"), false, cdato);

                BinaryReader reader = new BinaryReader(File.Open(rom, FileMode.Open, FileAccess.Read, FileShare.Read));

                uint o = SNESToPC(HasHeader(rom),false, 0xE8000);

                reader.BaseStream.Seek(o+8, SeekOrigin.Begin);
                o = reader.ReadUInt32();
                o &= 0x00FFFFFF;

                o = SNESToPC(HasHeader(rom),false, o);
                reader.BaseStream.Seek(o, SeekOrigin.Begin);

                uint dato=0;


                for(; ; )
                {
                    dato = reader.ReadUInt32();
                    o += 3;

                    dato &= 0x00FFFFFF;

                    if (dato == 0xFFFFFF) break;

                    if (dato != 0)
                    {

                        dato = SNESToPC(HasHeader(rom), false, dato);

                        reader.BaseStream.Seek(dato, SeekOrigin.Begin);
                        ushort size = reader.ReadUInt16();

                        creader.BaseStream.Seek(cdato, SeekOrigin.Begin);
                        ushort csize = creader.ReadUInt16();

                        if (size == csize && size > 24)
                        {
                            for(ushort i = 0; i < 24; i++)
                            {
                                reader.ReadByte();
                                creader.ReadByte();
                                size--;
                                csize--;
                            }

                            uint diff = 0;

                            for (ushort i = 0; i < size; i++)
                            {
                                byte b, cb;
                                b = reader.ReadByte();
                                cb = creader.ReadByte();

                                if (b != cb)
                                {
                                    diff++;
                                }
                            }

                            if(diff < size / 10) {
                                reader.Close();
                                creader.Close();
                                return slot;
                            }
                        }
                    }

                    slot++;
                    reader.BaseStream.Seek(o, SeekOrigin.Begin);
                }

                reader.Close();
                creader.Close();
            }

            catch (Exception)
            {
                return -1;
            }
            return -1;
        }

    }
}

