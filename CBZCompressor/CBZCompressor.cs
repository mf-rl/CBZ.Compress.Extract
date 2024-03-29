﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.IO.Compression;
using SevenZip;
using System.Net;
using static CBZCompressor.Common;
using System.Drawing;
using System.Drawing.Imaging;

namespace CBZCompressor
{
    public partial class CBZCompressor : Form
    {
        ProcessMode processMode = new ProcessMode();
        string defaultExt = ".cbr_.cbz";
        public CBZCompressor()
        {
            InitializeComponent();
        }
        private void LoadFolders_Click(object sender, EventArgs e)
        {
            coverImage.Image = null;
            string[] subdirectoryEntries = Directory.GetDirectories(pathFolders.Text).OrderBy(o => o).ToArray();
            originFolders.Items.Clear();
            originFolderFull.Items.Clear();
            destinationFiles.Items.Clear();
            originFolders.Items.AddRange(subdirectoryEntries.ToList().Select(s => Path.GetFileName(s)).ToArray());
            originFolderFull.Items.AddRange(subdirectoryEntries);
            destinationFiles.Items.AddRange(subdirectoryEntries.ToList().Select(s => string.Concat(Path.GetFileName(s), ".cbz")).ToArray());
            StatusMessage.Text = string.Format("Total folders: {0}", subdirectoryEntries.Count().ToString());
        }
        private void OriginFolders_SelectedIndexChanged(object sender, EventArgs e)
        {
            originFolderFull.SelectedIndex = destinationFiles.SelectedIndex = originFolders.SelectedIndex;
            if (Directory.Exists(originFolderFull.Text))
            {
                string firstFile = Directory.GetFiles(originFolderFull.Text).OrderBy(f => f).FirstOrDefault();
                ShowTempCover(firstFile);
            }            
        }
        private void ShowTempCover(string path)
        {
            coverImage.Image = null;
            using (Image img = Image.FromFile(path))
            {
                Image imgTmp = resizeImage(img, new Size(new Point { X = 177, Y = 250 }));
                coverImage.Image = imgTmp;
            }                
        }
        public static Image resizeImage(Image imgToResize, Size size)
        {
            return new Bitmap(imgToResize, size);
        }
        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == tabControl1.TabPages["tabPage1"]) processMode = ProcessMode.Compress;
            if (tabControl1.SelectedTab == tabControl1.TabPages["tabPage2"]) processMode = ProcessMode.Extract;
        }
        private void CompressAll_Click_1(object sender, EventArgs e)
        {

            BackgroundWorker bw = new BackgroundWorker();            
            switch (processMode)
            {
                case ProcessMode.Compress:
                    //bw.DoWork += Compress;
                    Compress();
                    break;
                case ProcessMode.Extract:
                    bw.DoWork += Extract;
                    bw.RunWorkerCompleted += ExtractFinished;
                    //Extract();
                    break;
            }
            bw.RunWorkerAsync();
        }
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        private void Extract(object sender, DoWorkEventArgs e)
        {
            SevenZipBase.SetLibraryPath(@"C:\Program Files\7-Zip\7z64.dll");
            string[] fileNames = originFiles.Items.OfType<string>().ToArray();
            progress.Invoke((Action)delegate
            {
                progress.Maximum = fileNames.Count();
                progress.Value = 0;
            });
            fileNames.ToList().ForEach(s =>
            {
                originFiles.Invoke((Action)delegate
                {
                    originFiles.Text = s;
                });
                var destinationFile = string.Empty;
                using (var file = new SevenZipExtractor(originFilesFull.Text))
                {
                    destinationFiles.Invoke((Action)delegate
                    {
                        destinationFile = destinationFolders.Text;
                    });
                    file.ExtractArchive(destinationFile);
                    progress.Invoke((Action)delegate
                    {
                        progress.Value++;
                    });
                }
                if (!string.IsNullOrEmpty(destinationFile)) CheckNestedContent(destinationFile);

                if (checkBox3.Checked) File.Delete(originFilesFull.Text);
            });
        }
        private void ExtractFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("Done! :)");
            originFilesFull.Items.Clear();
            destinationFolders.Items.Clear();
            originFiles.Items.Clear();
            progress.Value = 0;
        }
        private void CheckNestedContent(string path)
        {
            var nestedDirectories = Directory.GetDirectories(path);
            if (nestedDirectories.Any())
            {
                nestedDirectories.ToList().ForEach(d =>
                {
                    var files = Directory.GetFiles(d);
                    if (files.Any())
                        files.ToList().ForEach(f =>
                        {
                            string destFile = Path.Combine(path, Path.GetFileName(f));
                            if (!File.Exists(destFile)) File.Move(f, destFile);
                        });
                    Directory.Delete(d);
                });
                CheckNestedContent(path);
            }
        }
        public string DebugContent(string path)
        {
            string[] files = Directory.GetFiles(path);
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            Parallel.ForEach(files, p =>
            {
                Bitmap b = (Bitmap)Image.FromFile(p);
                if (b.Width > b.Height) b.RotateFlip(RotateFlipType.Rotate90FlipNone);
                string newPath = Path.Combine(tempPath, string.Concat(Path.GetFileNameWithoutExtension(p), ".png"));
                b.Save(newPath, ImageFormat.Png);
                b.Dispose();
            });
            files = Directory.GetFiles(tempPath);
            Parallel.ForEach(files, p =>
            {
                Bitmap b = (Bitmap)Image.FromFile(p);
                Encoder myEncoder = Encoder.Quality;
                EncoderParameters myEncoderParameters = new EncoderParameters(1);
                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 100L);
                myEncoderParameters.Param[0] = myEncoderParameter;
                string newJpgPath = Path.Combine(tempPath, string.Concat(Path.GetFileNameWithoutExtension(p), ".jpg"));
                b.Save(newJpgPath, GetEncoder(ImageFormat.Jpeg), myEncoderParameters);
                b.Dispose();
                File.Delete(p);
            });
            return tempPath;            
        }
        //private void Compress(object sender, DoWorkEventArgs e)
        private void Compress()
        {
            string[] folderNames = originFolders.Items.OfType<string>().ToArray();
            progress.Maximum = folderNames.Count();
            progress.Value = 0;
            string sFileToZip = string.Empty;
            bool[] resultado = folderNames.Select(s =>
            {
                bool actualRes = false;
                try
                {
                    originFolders.Text = s;
                    sFileToZip = checkBox1.Checked ? DebugContent(originFolderFull.Text) : originFolderFull.Text;
                    string sZipFile = string.Concat(originFolderFull.Text, ".cbz");
                    ZipFile.CreateFromDirectory(sFileToZip, sZipFile, System.IO.Compression.CompressionLevel.Optimal, false);
                    progress.Value++;
                    actualRes = true;
                }
                catch(Exception ex)
                {
                    actualRes = false;
                }
                if (checkBox2.Checked && actualRes) Directory.Delete(sFileToZip, true);
                return actualRes;
            }).ToArray();
            if (resultado.Any(x => !x))
                MessageBox.Show(string.Format("Errores found. {0} folders not compressed.", resultado.Count(x => !x)));
            else
                MessageBox.Show("Done! :)");
            originFolderFull.Items.Clear();  
            destinationFiles.Items.Clear(); 
            originFolders.Items.Clear();
            progress.Value = 0;
        }

        private void LoadCbrs_Click(object sender, EventArgs e)
        {
            LoadCBRFiles();
        }
        private void LoadCBRFiles()
        {
            originFiles.Items.Clear();
            destinationFolders.Items.Clear();
            string[] subdirectoryEntries = Directory.GetFiles(pathCbrs.Text).Where(f => defaultExt.ToLower().Contains(Path.GetExtension(f).ToLower())).ToArray();
            originFilesFull.Items.AddRange(subdirectoryEntries);
            originFiles.Items.AddRange(subdirectoryEntries.ToList()                
                .Select(s => Path.GetFileNameWithoutExtension(s)).ToArray());
            destinationFolders.Items.AddRange(subdirectoryEntries.ToList().Select(s =>
            {
                string directory = Path.GetDirectoryName(s);
                string file = FormatName(Path.GetFileNameWithoutExtension(s));
                return Path.Combine(directory, file);
            }).ToArray());
            StatusMessage.Text = string.Format("Total folders: {0}", subdirectoryEntries.Count().ToString());
        }
        private string FormatName(string rawName)
        {
            string formatedName = rawName;

            if (cbrNameExceps.Items.Count > 0)
            {
                List<string> exceptions = cbrNameExceps.Items.OfType<string>().ToList();
                exceptions.ForEach(x =>
                {
                    formatedName = formatedName.Replace(x, string.Empty).Trim();
                });
            }

            return formatedName;
        }

        private void originFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            originFilesFull.SelectedIndex = destinationFolders.SelectedIndex = originFiles.SelectedIndex;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            cbrNameExceps.Items.Add(cbrExc.Text.Trim());
            cbrExc.Text = string.Empty;
            LoadCBRFiles();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            cbrNameExceps.Items.Clear();
        }
    }
}
