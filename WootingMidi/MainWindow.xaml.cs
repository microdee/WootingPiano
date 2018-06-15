using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Commons.Music.Midi;
using HidSharp;

namespace WootingMidi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LowOctave.FillKeys(_lowOctKeys);
            HighOctave.FillKeys(_highOctKeys);
            UpdateMidi();
            UpdateHid();
            DeviceList.Local.Changed += (sender, args) => UpdateHid();
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (_hidStream == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    try
                    {
                        var bytes = _hidStream.Read();
                        if (bytes.Length < 2) continue;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (_hidStream == null) return;
                            HidStatus.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                            for (int i = 0; i < 24; i++)
                            {
                                _keyPress[i] = 0;
                            }
                            for (int i = 0; i < (bytes.Length - 1) / 2; i++)
                            {
                                int ii = i * 2 + 1;
                                if(bytes[ii] == 0) continue;
                                if (_keyIndex.Contains(bytes[ii]))
                                {
                                    var ki = _keyIndex.IndexOf(bytes[ii]);
                                    _keyPress[ki] = (((double) bytes[ii + 1]) / 255.0);
                                }
                            }

                            ProcessNotes();

                            var looffs = 12 + _lowOctVal * 12;
                            var hioffs = 12 + _hiOctVal * 12;

                            LowOctave.UpdateBars(_notes, looffs);
                            HighOctave.UpdateBars(_notes, hioffs);
                        }));
                    }
                    catch (Exception e)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            HidStatus.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                        }));
                        throw e;
                    }
                    Thread.Sleep(1);
                }
            });
        }

        private IMidiPortDetails[] _midiOutDetails;
        private HidDevice[] _hidDevices;
        private readonly List<byte> _keyIndex = new List<byte> { 65, 50, 66, 51, 67, 68, 53, 69, 54, 70, 55, 71, 34, 19, 35, 20, 36, 37, 22, 38, 23, 39, 24, 40 };
        private readonly double[] _keyPress = new double[24];
        private string _lowOctKeys = "ZSXDCVGBHNJM";
        private string _highOctKeys = "W3E4RT6Y7U8I";

        private int _lowOctVal = 0;
        private int _hiOctVal = 1;

        private readonly NoteKey[] _notes = new NoteKey[128];
        private readonly byte[] _rawMidiMessageBuf = new byte[3];

        private void ProcessNotes()
        {
            if(_midiDevice == null) return;

            var looffs = 12 + _lowOctVal * 12;
            var hioffs = 12 + _hiOctVal * 12;
            for (int i = 0; i < 128; i++)
            {
                if(_notes[i] == null) _notes[i] = new NoteKey() { NoteId = (uint)i };
                RawMidiMessage res;
                if (i >= looffs && i < looffs + 12)
                {
                    var ii = i - looffs;
                    res = _notes[i].Submit(_keyPress[ii]);
                }
                else if (i >= hioffs && i < hioffs + 12)
                {
                    var ii = i - hioffs;
                    res = _notes[i].Submit(_keyPress[ii + 12]);
                }
                else
                {
                    res = _notes[i].Submit(0.0);
                }

                if (res.Valid)
                {
                    res.Bytes(_rawMidiMessageBuf, 0);
                    _midiDevice.Send(_rawMidiMessageBuf, 0, 3, 0);
                }
            }
        }

        private void UpdateMidi()
        {
            _midiOutDetails = MidiAccessManager.Default.Outputs.ToArray();
            MidiSelect.Items.Clear();
            foreach (var midiOutDetail in _midiOutDetails)
            {
                MidiSelect.Items.Add(midiOutDetail.Name);
            }
        }

        private void UpdateHid()
        {
            if (_hidStream != null)
            {
                _hidStream.Close();
                _hidStream.Dispose();
            }
            bool success = DeviceList.Local.TryGetHidDevice(out _hidDevice, 1003, 65281);
            if (success)
            {
                _hidStream = _hidDevice.Open();
                HidStatus.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0));
            }
            else
            {
                HidStatus.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                _hidDevice = null;
            }
        }

        private IMidiOutput _midiDevice;
        private HidDevice _hidDevice;
        private HidStream _hidStream;

        private async void OnMidiSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_midiDevice != null)
            {
                await _midiDevice.CloseAsync().ConfigureAwait(false);
            }
            var deviceinfo = _midiOutDetails[MidiSelect.SelectedIndex];
            _midiDevice = await MidiAccessManager.Default.OpenOutputAsync(deviceinfo.Id).ConfigureAwait(false);
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void LowOctDecr_Click(object sender, RoutedEventArgs e)
        {
            _lowOctVal--;
            if (_lowOctVal < -1) _lowOctVal = -1;
            LowOctLabel.Text = _lowOctVal.ToString();
        }

        private void HighOctDecr_Click(object sender, RoutedEventArgs e)
        {
            _hiOctVal--;
            if (_hiOctVal < -1) _hiOctVal = -1;
            HighOctLabel.Text = _hiOctVal.ToString();
        }

        private void LowOctIncr_Click(object sender, RoutedEventArgs e)
        {
            _lowOctVal++;
            if (_lowOctVal > 9) _lowOctVal = 9;
            LowOctLabel.Text = _lowOctVal.ToString();
        }

        private void HighOctIncr_Click(object sender, RoutedEventArgs e)
        {
            _hiOctVal++;
            if (_hiOctVal > 9) _hiOctVal = 9;
            HighOctLabel.Text = _hiOctVal.ToString();
        }
    }
}
