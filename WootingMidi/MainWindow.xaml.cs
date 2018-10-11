using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Xaml;
using Commons.Music.Midi;
using HidSharp;
using HidSharp.Reports;

namespace WootingMidi
{
    public class CcKeyPress
    {
        private double _press;
        public string KeyCode { get; set; }

        public double Press
        {
            get => _press;
            set
            {
                if(value > 0) Active = true;
                _press = value;
            }
        }

        public bool Active { get; set; }
    }

    public class Settings
    {
        public static Settings Last;

        public int Octave1 { get; set; } = 0;
        public int Octave2 { get; set; } = 1;
        public string MidiDevice { get; set; } = "";
        public int Channel { get; set; } = 0;
        public bool SendNote { get; set; } = true;
        public bool SendAt { get; set; } = true;
        public bool SendCc { get; set; } = true;

        public Settings()
        {
            Last = this;
        }

        public static string Path()
        {
            return System.IO.Path.Combine(Environment.CurrentDirectory, "settings.xaml");
        }

        public void Save()
        {
            XamlServices.Save(Path(), this);
        }

        public static Settings Load()
        {
            try
            {
                return Last = XamlServices.Load(Path()) as Settings ?? new Settings();
            }
            catch (Exception e)
            {
                return new Settings();
            }
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public const byte NOKEY = 255;
        public const int WOOTING_ONE_VID = 0x03EB;
        public const int WOOTING_ONE_PID = 0xFF01;
        public const int WOOTING_ONE_ANALOG_USAGE_PAGE = 0x1338;

        private Settings _settings = new Settings();
        private bool _loaded = false;

        public static readonly DependencyProperty ChannelNumberProperty =
            DependencyProperty.Register("ChannelNumber", typeof(int), typeof(MainWindow), new PropertyMetadata(0));

        public int ChannelNumber
        {
            get => (int) GetValue(ChannelNumberProperty);
            set => SetValue(ChannelNumberProperty, value);
        }

        public static readonly DependencyProperty LowOctaveNumProperty =
            DependencyProperty.Register("LowOctaveNum", typeof(int), typeof(MainWindow), new PropertyMetadata(0));

        public int LowOctaveNum
        {
            get => (int)GetValue(LowOctaveNumProperty);
            set => SetValue(LowOctaveNumProperty, value);
        }

        public static readonly DependencyProperty HiOctaveNumProperty =
            DependencyProperty.Register("HiOctaveNum", typeof(int), typeof(MainWindow), new PropertyMetadata(1));

        public int HiOctaveNum
        {
            get => (int)GetValue(HiOctaveNumProperty);
            set => SetValue(HiOctaveNumProperty, value);
        }

        public static readonly DependencyProperty SendNoteProperty =
            DependencyProperty.Register("SendNote", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        public bool SendNote
        {
            get => (bool)GetValue(SendNoteProperty);
            set => SetValue(SendNoteProperty, value);
        }

        public static readonly DependencyProperty SendAtProperty =
            DependencyProperty.Register("SendAt", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        public bool SendAt
        {
            get => (bool)GetValue(SendAtProperty);
            set => SetValue(SendAtProperty, value);
        }

        public static readonly DependencyProperty SendCcProperty =
            DependencyProperty.Register("SendCc", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        public bool SendCc
        {
            get => (bool)GetValue(SendCcProperty);
            set => SetValue(SendCcProperty, value);
        }

        public ObservableDictionary<int, CcKeyPress> CcPresses { get; } = new ObservableDictionary<int, CcKeyPress>();
        public ObservableCollection<CcKeyPress> CcPressesList { get; } = new ObservableCollection<CcKeyPress>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LowOctave.FillKeys(_lowOctKeys);
            HighOctave.FillKeys(_highOctKeys);
            
            _settings = Settings.Load();

            ChannelNumber = _settings.Channel;
            LowOctaveNum = _settings.Octave1;
            HiOctaveNum = _settings.Octave2;
            SendNote = _settings.SendNote;
            SendAt = _settings.SendAt;
            SendCc = _settings.SendCc;

            UpdateMidi();
            UpdateHid();
            DeviceList.Local.Changed += (sender, args) => UpdateHid();

            _loaded = true;

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
                            foreach (var cc in CcPresses.Values)
                            {
                                cc.Press = 0.0;
                            }
                            HidStatus.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                            for (int i = 0; i < 24; i++)
                            {
                                _keyPress[i] = 0;
                            }
                            for (int i = 0; i < (bytes.Length - 1) / 2; i++)
                            {
                                int ii = i * 2 + 1;
                                if(bytes[ii] == 0) continue;
                                if (CcPresses.ContainsKey(bytes[ii]))
                                {
                                    CcPresses[bytes[ii]].KeyCode = bytes[ii].ToString();
                                    CcPresses[bytes[ii]].Press = (((double) bytes[ii + 1]) / 255.0) * 100.0;
                                }
                                else
                                {
                                    CcPresses.Add(bytes[ii], new CcKeyPress()
                                    {
                                        KeyCode = bytes[ii].ToString(),
                                        Press = (((double)bytes[ii + 1]) / 255.0) * 100.0
                                    });
                                }

                                if (_keyIndex.Contains(bytes[ii]))
                                {
                                    var ki = _keyIndex.IndexOf(bytes[ii]);
                                    _keyPress[ki] = (((double) bytes[ii + 1]) / 255.0);
                                }
                            }

                            ProcessNotes();

                            CcPresses.OnCollectionChanged();
                            CcPressesList.Clear();
                            foreach (var press in CcPresses.Values)
                            {
                                if(press.Press > 0.0)
                                    CcPressesList.Add(press);
                            }

                            var looffs = 12 + LowOctaveNum * 12;
                            var hioffs = 12 + HiOctaveNum * 12;

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
                    Thread.Sleep(2);
                }
            });
        }

        private IMidiPortDetails[] _midiOutDetails;
        private HidDevice[] _hidDevices;
        private readonly List<byte> _keyIndex = new List<byte> { 65, 50, 66, 51, 67, 68, 53, 69, 54, 70, 55, 71, 34, 19, 35, 20, 36, 37, 22, 38, 23, 39, 24, 40 };
        private readonly double[] _keyPress = new double[24];
        private string _lowOctKeys = "ZSXDCVGBHNJM";
        private string _highOctKeys = "W3E4RT6Y7U8I";

        private readonly NoteKey[] _notes = new NoteKey[128];
        private readonly byte[] _rawMidiMessageBuf = new byte[3];

        private void ProcessNotes()
        {
            if(_midiDevice == null) return;

            var looffs = 12 + LowOctaveNum * 12;
            var hioffs = 12 + HiOctaveNum * 12;
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
                    _midiDevice.Send(_rawMidiMessageBuf, 0, 3, 0 /*(long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds*/);
                }
            }

            foreach (var ccc in CcPresses)
            {
                var cc = MidiCc.Generate(ChannelNumber, ccc.Key, ccc.Value.Press / 100.0);
                if (!cc.Valid || !ccc.Value.Active) continue;
                cc.Bytes(_rawMidiMessageBuf, 0);
                _midiDevice.Send(_rawMidiMessageBuf, 0, 3, 0 /*(long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds*/);
                ccc.Value.Active = false;
            }

            if (SendAt)
            {
                var cat = MidiChannelAt.Generate(ChannelNumber, _notes.Max(cp => cp.Pressure));
                cat.Bytes(_rawMidiMessageBuf, 0);
                _midiDevice.Send(_rawMidiMessageBuf, 0, 2, 0 /*(long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds*/);
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
            MidiSelect.SelectedItem = _settings.MidiDevice;
        }

        private void UpdateHid()
        {
            if (_hidStream != null)
            {
                _hidStream.Close();
                _hidStream.Dispose();
            }

            var devices = DeviceList.Local.GetHidDevices(WOOTING_ONE_VID, WOOTING_ONE_PID);
            var success = false;
            foreach (var candidate in devices)
            {
                try
                {
                    if (candidate.GetMaxInputReportLength() == 33)
                    {
                        success = true;
                        _hidDevice = candidate;
                        break;
                    }
                }
                catch (Exception e)
                { }
            }
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

        private void OnMidiSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_midiDevice != null)
            {
                var closetask = _midiDevice.CloseAsync();
                closetask.Wait();
            }
            var deviceinfo = _midiOutDetails[MidiSelect.SelectedIndex];
            var opentask = MidiAccessManager.Default.OpenOutputAsync(deviceinfo.Id);
            opentask.Wait();
            _midiDevice = opentask.Result;
            _settings.MidiDevice = deviceinfo.Name;
            _settings.Save();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (_loaded &&
                (e.Property == LowOctaveNumProperty ||
                 e.Property == HiOctaveNumProperty ||
                 e.Property == ChannelNumberProperty ||
                 e.Property == SendNoteProperty ||
                 e.Property == SendAtProperty ||
                 e.Property == SendCcProperty)
            )
            {
                _settings.Channel = ChannelNumber;
                _settings.Octave1 = LowOctaveNum;
                _settings.Octave2 = HiOctaveNum;
                _settings.SendNote = SendNote;
                _settings.SendAt = SendAt;
                _settings.SendCc = SendCc;

                foreach (var note in _notes)
                {
                    note.Channel = (uint)ChannelNumber;
                }
                _settings.Save();
            }
            base.OnPropertyChanged(e);
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void LowOctDecr_Click(object sender, RoutedEventArgs e)
        {
            LowOctaveNum = Math.Max(0, LowOctaveNum - 1);
        }

        private void HighOctDecr_Click(object sender, RoutedEventArgs e)
        {
            HiOctaveNum = Math.Max(0, HiOctaveNum - 1);
        }

        private void LowOctIncr_Click(object sender, RoutedEventArgs e)
        {
            LowOctaveNum = Math.Min(9, LowOctaveNum + 1);
        }

        private void HighOctIncr_Click(object sender, RoutedEventArgs e)
        {
            HiOctaveNum = Math.Min(9, HiOctaveNum + 1);
        }
    }
}
