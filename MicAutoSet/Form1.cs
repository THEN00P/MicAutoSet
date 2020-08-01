using System;
using System.Windows.Forms;
using NAudio.Mixer;
using NAudio.CoreAudioApi;

namespace MicAutoSet
{
    public partial class Form1 : Form
    {
        private UnsignedMixerControl volumeControl;
        public int volume = 0;
        MMDevice device;

        public Form1()
        {
            this.WindowState = FormWindowState.Minimized;

            InitializeComponent();

            int waveInDeviceNumber = 0;
            MixerLine mixerLine = new MixerLine((IntPtr)waveInDeviceNumber,
                                           0, MixerFlags.WaveIn);

            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);

            device.AudioEndpointVolume.OnVolumeNotification += audioModifyEvent;

            foreach (MixerControl control in mixerLine.Controls)
            {
                if (control.ControlType == MixerControlType.Volume)
                {
                    volumeControl = control as UnsignedMixerControl;
                    break;
                }
            }

            trackBar1.Value = (int) Math.Round(volumeControl.Percent);
            volume = trackBar1.Value;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Hide();
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            volume = trackBar1.Value;
            label2.Text = Convert.ToString(volume);

            audioModify();
        }

        private void audioModifyEvent(AudioVolumeNotificationData data)
        {
            if (Math.Round(data.MasterVolume * 100) != volume) audioModify();
        }

        private void audioModify()
        {
            volumeControl.Percent = volume;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if(this.WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon1.Visible = true;
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://twitter.com/THEN00P");
        }
    }
}
