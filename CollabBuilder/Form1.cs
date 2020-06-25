using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;


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
                    
                    if(valid && words[0] == level)
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
                        if (i >= 8 && i <= 11) continue;
                    }
                    if (!ltbypass)
                    {
                        if (i == 1) continue;
                    }

                    o = o & 0x0FFF;
                    if (o > 0x7F)
                    {
                        Graphics.Rows.Add(level, sublevel, GFXSlots[i], gfx.ToString("X"), o.ToString("X"));
                        gfx++;
                    }
                }
            }
            catch (Exception e)
            {

            }
            reader.Close();

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

            if(range != "")
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
                                        mh = (o3 & 0xF0)/16;
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

        private string GetMWLMap16BG(string path, ref string range)
        {

            int o;
            int min, max;
            int m16;
            min = 0;
            max = 0;

            if (range != "")
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
                    for(int i=0;i<1024;i++)
                    {
                        m16 = reader.ReadInt16();
                        m16 |= 0x8000;

                        if (m16 < min || min == 0) min = m16 & 0xFF00;
                        if (((m16) | 0xFF) > max || max == 0) max = ((m16) | 0xFF);

                        range = min.ToString("X") + "-" + max.ToString("X");

                    }
                }
            }
            catch (Exception e)
            {

            }

            reader.Close();

            return range;
        }


        private void button1_Click(object sender, EventArgs e)
        {


            OpenFileDialog dialog = new OpenFileDialog()
            {
                Filter = "Text files (*.smc)|*.smc",
                Title = "Open SMW ROM"
            };
            dialog.Multiselect = false;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
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
                //try
                // {
                string[] files = Directory.GetFiles(dir,"*.mwl");

 
                if (files.Length==0)
                {
                    MessageBox.Show("No mwl files found in Levels folder.");
                    return;
                }

                int len = 0;
                int l = 1;
                int sl = 257;

                int bl = 0x300;
                int blbg = 0x8000;
                int del = 0;

                int spr = 0;
                int ext = 0;
                int clu = 0;
                int sho = 0xC0;
                int gen = 0xD0;
                int spmode = 0;

                int mus = 0x29;
                int gfx = 0x80;

                StreamReader text;
                string line;
                string[] words;
                string item;
                string item2;

                string m16 = "";
                string m16bg = "";

                foreach (string path in files)
                {
                    m16 = "";
                    string file = Path.GetFileName(path);

                    string level = GetMWLLevel(path);

                    string folder = Path.GetFileNameWithoutExtension(path);

                    string sub = Path.GetDirectoryName(path);
                    sub += "\\";
                    sub += folder;

                    item = GetUberASM(sub,level);

                    GetMWLMap16(path, ref m16);
                    GetMWLMap16BG(path, ref m16bg);

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

                            sub = Path.GetDirectoryName(path);
                            sub += "\\";
                            sub += folder;

                            item = GetUberASM(sub, level);

                            GetMWLMap16(subpath, ref m16);
                            GetMWLMap16BG(subpath, ref m16bg);

                            Levels.Rows.Add(l.ToString("X"), sl.ToString("X"), level, folder + "\\Sublevels\\" + file,GetMWLMusic(subpath),item);

                            GetMWLGraphics(subpath, l.ToString("X"), sl.ToString("X"), ref gfx);

                            sl++;
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
                                    } else
                                    {

                                        int r1 = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);

                                        if (spmode == 0)
                                        {
                                            if (r1 < 0xB0)
                                            {
                                                Sprites.Rows.Add(l.ToString("X"),"Sprite", spr.ToString("X2"), words[0], item);

                                                spr++;
                                            }
                                            else if (r1 < 0xD0)
                                            {
                                                Sprites.Rows.Add(l.ToString("X"), "Sprite", sho.ToString("X2"), words[0], item);

                                                sho++;
                                            }
                                            else
                                            {
                                                Sprites.Rows.Add(l.ToString("X"), "Sprite", gen.ToString("X2"), words[0], item);

                                                gen++;

                                            }
                                        }
                                        if (spmode == 1)
                                        {
                                            Sprites.Rows.Add(l.ToString("X"), "Extended", ext.ToString("X2"), words[0], item);

                                            ext++;
                                        }
                                        if (spmode == 2)
                                        {
                                            Sprites.Rows.Add(l.ToString("X"), "Cluster", clu.ToString("X2"), words[0], item);

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
                        Map16.Rows.Add(l.ToString("X"), "FG","", "", m16.Split('-')[0], m16.Split('-')[1]);

                    }
                    else
                    {
                        del = int.Parse(m16.Split('-')[0], System.Globalization.NumberStyles.HexNumber) - bl;
                        del *= -1;
                        len = int.Parse(m16.Split('-')[1], System.Globalization.NumberStyles.HexNumber) - int.Parse(m16.Split('-')[0], System.Globalization.NumberStyles.HexNumber);
                        Map16.Rows.Add(l.ToString("X"), "FG",bl.ToString("X"), (bl + len).ToString("X"), m16.Split('-')[0], m16.Split('-')[1]);

                        bl += len + 1;
                    }




                    if (len>0)
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


                    if (m16bg == "")
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
                    }


                    ROM.Text = dialog.FileName;

                /*}
                catch (Exception ex)
                {
                    ROM.Text = "";
                    MessageBox.Show("Could not load levels from Levels folder.");
                    return;
                }*/

            }

        }
    }
}
