﻿using GenshinAccount.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GenshinAccount
{
    public partial class FormMain : Form
    {
        private readonly string userDataPath = Path.Combine(Application.StartupPath, "UserData");
        private string thisVersion;

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // 标题加上版本号
            string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if (currentVersion.Length > 3)
            {
                thisVersion = currentVersion.Substring(0, 3);
                currentVersion = " v" + thisVersion;
            }
            this.Text += currentVersion;
            GAHelper.Instance.RequestPageView($"/acct/main/{thisVersion}", $"进入{thisVersion}版本原神账户切换工具主界面");


            chkAutoStartYS.Checked = Properties.Settings.Default.AutoRestartYSEnabled;
            chkSkipTips.Checked = Properties.Settings.Default.SkipTipsEnabled;

            if (string.IsNullOrEmpty(Properties.Settings.Default.YSInstallPath))
            {
                txtPath.Text = FindInstallPathFromRegistry();
            }
            else
            {
                txtPath.Text = Properties.Settings.Default.YSInstallPath;
            }

            lvwAcct.Columns[0].Width = lvwAcct.Width;
            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(10, 20);
            lvwAcct.SmallImageList = imageList;
            RefreshList();

        }

        private void btnSaveCurr_Click(object sender, EventArgs e)
        {
            FormInput form = new FormInput();
            form.ShowDialog();
            RefreshList();
        }

        private void RefreshList()
        {
            if (!Directory.Exists(userDataPath))
            {
                Directory.CreateDirectory(userDataPath);
            }
            lvwAcct.Items.Clear();
            DirectoryInfo root = new DirectoryInfo(userDataPath);
            FileInfo[] files = root.GetFiles();
            foreach (FileInfo file in files)
            {
                lvwAcct.Items.Add(new ListViewItem()
                {
                    Text = file.Name
                });
            }

            if (lvwAcct.Items.Count > 0)
            {
                btnDelete.Enabled = true;
                btnSwitch.Enabled = true;
            }
            else
            {
                btnDelete.Enabled = false;
                btnSwitch.Enabled = false;
            }
        }

        private void btnSwitch_Click(object sender, EventArgs e)
        {
            if (lvwAcct.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = lvwAcct.SelectedItems[0]?.Text;
            Switch(name, chkAutoStartYS.Checked);
        }

        private void Switch(string name, bool autoRestart)
        {
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!autoRestart)
            {
                if (YuanShenIsRunning())
                {
                    MessageBox.Show("原神正在运行，请先关闭原神进程后再切换账号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (chkSkipTips.Checked || MessageBox.Show($"是否要切换为[{name}]", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (autoRestart)
                {
                    var pros = Process.GetProcessesByName("YuanShen");
                    if (pros.Any())
                    {
                        pros[0].Kill();
                    }
                }
                YSAccount acct = YSAccount.ReadFromDisk(name);
                acct.WriteToRegedit();

                if (autoRestart)
                {
                    if (string.IsNullOrEmpty(txtPath.Text))
                    {
                        MessageBox.Show("请选择原神安装路径后，才能使用自动重启功能", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        chkAutoStartYS.Checked = false;
                    }
                    else
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.UseShellExecute = true;
                        startInfo.WorkingDirectory = Environment.CurrentDirectory;
                        startInfo.FileName = Path.Combine(txtPath.Text, "Genshin Impact Game", "YuanShen.exe");
                        startInfo.Verb = "runas";
                        Process.Start(startInfo);
                    }
                }

                if (!chkSkipTips.Checked)
                {
                    MessageBox.Show($"账户[{name}]切换成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lvwAcct.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = lvwAcct.SelectedItems[0]?.Text;
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请选择要切换的账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            YSAccount.DeleteFromDisk(name);
            RefreshList();
        }

        private bool YuanShenIsRunning()
        {
            var pros = Process.GetProcessesByName("YuanShen");
            if (pros.Any())
            {
                return true;
            }
            else
            {
                pros = Process.GetProcessesByName("GenshinImpact");
                return pros.Any();
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/babalae/genshin-account");
        }

        private void lvwAcct_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo info = lvwAcct.HitTest(e.X, e.Y);
            if (info.Item != null)
            {
                Switch(info.Item.Text, chkAutoStartYS.Checked);
            }
        }

        /// <summary>
        /// 从注册表中寻找安装路径
        /// </summary>
        /// <param name="uninstallKeyName">
        /// 安装信息的注册表键名 原神
        /// </param>
        /// <returns>安装路径</returns>
        public static string FindInstallPathFromRegistry()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\原神"))
                {
                    if (key == null)
                    {
                        return null;
                    }
                    object installLocation = key.GetValue("InstallPath");
                    if (installLocation != null && !string.IsNullOrEmpty(installLocation.ToString()))
                    {
                        return installLocation.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return null;
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.AutoRestartYSEnabled = chkAutoStartYS.Checked;
            Properties.Settings.Default.SkipTipsEnabled = chkSkipTips.Checked;
            Properties.Settings.Default.YSInstallPath = txtPath.Text;
            Properties.Settings.Default.Save();
        }

        private void btnChoosePath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择原神安装路径";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath) || !File.Exists(Path.Combine(dialog.SelectedPath, "Genshin Impact Game", "YuanShen.exe")))
                {
                    MessageBox.Show("无法在该文件夹中找到原神启动程序，请选择正确的原神安装路径!");
                }
                else
                {
                    txtPath.Text = dialog.SelectedPath;
                }
            }
        }
    }
}
