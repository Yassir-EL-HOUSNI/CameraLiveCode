using LandInstruments.SST.Common;
using LandInstruments.SST.DeviceLayer.DeviceLevel1;
using LandInstruments.SST.DeviceLayer.DeviceLevel3.FTIE1000SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using System.Timers;
using System.Globalization;
using System.Data;
using System.Data.OleDb;
using System.IO;
namespace CameraLive
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class LiveWindow : Window
    {
        /// <summary>
        /// sdk instance for discovery
        /// </summary>
        private FTIE1000 _SDKDiscoveryInstance;

        /// <summary>
        /// SearchedDevices Delegate, 
        /// this delegate will be passed for the discovery handler
        /// </summary>
        FTIE1000.SearchedDevicesDelegate _sd;

        /// <summary>
        /// Map for storing the dicovered devices
        /// </summary>
        Dictionary<CommsSettings, string> _discoveredDeviceMap;

        /// <summary>
        /// SDK instance
        /// </summary>
        FTIE1000 _SDKInstance;

        /// <summary>
        /// WriteableBitmap object for image live show
        /// </summary>
        WriteableBitmap _bmp;

        /// <summary>
        /// /// Thread for receive the live packets
        /// </summary>
        Thread _liveThread;

        /// <summary>
        /// Temperature values array
        /// </summary>
        int[] _temperatureValues = null;

        /// <summary>
        /// Colour bitmap data
        /// </summary>
        int[] _bitmapData = null;

        /// <summary>
        /// Colour table, for converting temperature data in to colour bitmap
        /// </summary>
        ColourTable _palette = null;
        string path1 = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).ToString() + @"\'ESSAI_Du" + DateTime.Now.ToString() + ".txt";

        //public static System.IO.FileStream Create(path1);
        //private static System.IO.FileStream Create (),int);


        /// <summary>
        /// Colour Table Maximum Temperature 
        /// </summary>
        double _maximumTemperature = 5000.0;

        /// <summary>
        /// Colour Table Minimum temperature
        /// </summary>
        double _minimumTemperature = -20.0;

        /// <summary>
        /// Temperature Range of the colour table
        /// </summary>
        double _temperatureRange = 5020.0;

        /// <summary>
        /// Width of the video
        /// </summary>
        const short _videoWidth = 659;

        /// <summary>
        /// Height of the video
        /// </summary>
        const short _videoHeight = 494;

        /// <summary>
        /// constructor
        /// </summary>
        public LiveWindow()
        {

            InitializeComponent();

            //Create a SDK discovery instance
            _SDKDiscoveryInstance = new FTIE1000();

            //create the SearchedDevices Delegate, 
            //this delegate will be passed for the discovery handler
            _sd = new Device.SearchedDevicesDelegate(OnDiscovery);

            //Create a SDK instance
            _SDKInstance = new FTIE1000();

            //Initializing the writeable bitmap
            _bmp = BitmapFactory.New(_videoWidth, _videoHeight);
            _bmp.Clear(Colors.Black);
            imgVideoPane.Source = _bmp;

            //Initializing the temperature value array
            _temperatureValues = new int[_videoWidth * _videoHeight];

            //Initializing Colour bitmap data
            _bitmapData = new int[_videoWidth * _videoHeight];



        }

        /// <summary>
        /// On window load, initiate the device discovery
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedEventArgs</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //start the search for the device in the network
                _SDKDiscoveryInstance.BeginSearchDevices(_sd, 10);
            }
            catch (DeviceSDKException sdkEx)
            {
                MessageBox.Show("Error Code :" + sdkEx.ErrorEventArgs.ErrorCode.ToString()
                    + "\n\n\n\nMessage :" + sdkEx.ErrorEventArgs.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n\n\nStack Trace :" + ex.StackTrace);
            }

        }




        /// <summary>
        /// On Clear button click
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedEventArgs</param>
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            //clear all the sub nodes of FTIE1000 Node
            TreeViewItem ftie1000Node = (TreeViewItem)trvDevices.Items[0];
            ftie1000Node.Items.Clear();
            txtIPAddress.Text = "Select a device above";

        }



        /// <summary>
        /// On Refersh button click
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedEventArgs</param>
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtIPAddress.Text = "Select a device above";

                //start the search for the device in the network
                _SDKDiscoveryInstance.BeginSearchDevices(_sd, 10);
                // Tmoy1 += _SDKInstance.GetTemperatureAt(0, 0);
            }
            catch (DeviceSDKException sdkEx)
            {
                MessageBox.Show("Error Code :" + sdkEx.ErrorEventArgs.ErrorCode.ToString()
                    + "\n\n\n\nMessage :" + sdkEx.ErrorEventArgs.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n\n\nStack Trace :" + ex.StackTrace);
            }
        }

        /// <summary>
        /// On tree view selection changed
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedPropertyChangedEventArgs</param>
        private void trvDevices_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem)
            {
                //Find the IP from the selected tree view item 
                string selectedNodeHeader = (e.NewValue as TreeViewItem).Header.ToString();
                if (selectedNodeHeader != "FTIE1000")
                    txtIPAddress.Text = selectedNodeHeader.Split('-')[0];
                else
                    txtIPAddress.Text = "Select a device above";
            }
        }

        /// <summary>
        /// On connect button click
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedEventArgs</param>
        /// 

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                TCPCommsSettings commSettings = null;

                //Get the selected device ip address from the device ip textbox
                IPAddress selectedIPAddress = IPAddress.Parse(txtIPAddress.Text);

                //Fetch the TCPCommSettings object from the discovered Device Map.
                foreach (var discoveredDeviceSettings in _discoveredDeviceMap.Keys)
                    if ((discoveredDeviceSettings as TCPCommsSettings).DeviceIPAddress.ToString() == selectedIPAddress.ToString())
                        commSettings = discoveredDeviceSettings as TCPCommsSettings;

                //Set the CommSettings for the SDK
                _SDKInstance.SetCommsSettings(commSettings);

                //Connect the instrument
                _SDKInstance.Connect();

                //_SDKInstance.SetHeartBeatTimeout(300000);
                //Yassir








                //Update the device connection status from the SDK
                txtConnectionStatus.Text = _SDKInstance.GetConnectionStatus().ToString();


                _SDKInstance.GetTemperatureAt(0, 0);





            }
            catch (DeviceSDKException sdkEx)
            {
                MessageBox.Show("Error Code :" + sdkEx.ErrorEventArgs.ErrorCode.ToString() + "\n\n\n\nMessage :" + sdkEx.ErrorEventArgs.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n\n\nStack Trace :" + ex.StackTrace);
            }
        }

        /// <summary>
        /// On Disconnect button click
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedEventArgs</param>
        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //disconnect the device
                _SDKInstance.Disconnect();

                //Update the device connection status from the SDK
                txtConnectionStatus.Text = _SDKInstance.GetConnectionStatus().ToString();
            }
            catch (DeviceSDKException sdkEx)
            {
                MessageBox.Show("Error Code :" + sdkEx.ErrorEventArgs.ErrorCode.ToString() +
                    "\n\n\n\nMessage :" + sdkEx.ErrorEventArgs.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n\n\nStack Trace :" + ex.StackTrace);
            }
        }



        /// <summary>
        /// On start live stream click
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedEventArgs</param>
        private void btnStartLive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Start the Live Image from the SDK to the client
                _SDKInstance.StartLiveImageAcquisition();
                txtConnectionStatus.Text = "Streaming";

                //Get the colour table information from the SDK
                _palette = _SDKInstance.GetPalette();

                _maximumTemperature = _palette.Maximum;
                _minimumTemperature = _palette.Minimum;
                _temperatureRange = _maximumTemperature - _minimumTemperature;

                //create the live thread
                _liveThread = new Thread(new ThreadStart(LiveThread));
                _liveThread.Start();
            }
            catch (DeviceSDKException sdkEx)
            {
                MessageBox.Show("Error Code :" + sdkEx.ErrorEventArgs.ErrorCode.ToString() +
                    "\n\n\n\nMessage :" + sdkEx.ErrorEventArgs.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n\n\nStack Trace :" + ex.StackTrace);
            }
        }

        /// <summary>
        /// On stop live stream click
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">RoutedEventArgs</param>
        private void btnStopLive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Stop the live thread
                if (_liveThread != null && _liveThread.IsAlive)
                    _liveThread.Abort();

                //Stop the Live Image from the SDK to the client
                _SDKInstance.StopLiveImageAcquisition();

                txtConnectionStatus.Text = _SDKInstance.GetConnectionStatus().ToString();
            }
            catch (DeviceSDKException sdkEx)
            {
                MessageBox.Show("Error Code :" + sdkEx.ErrorEventArgs.ErrorCode.ToString() +
                      "\n\n\n\nMessage :" + sdkEx.ErrorEventArgs.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n\n\nStack Trace :" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Discovery Handler
        /// </summary>
        /// <param name="e">DeviceSDKErrorEventArgs, null when no error</param>
        /// <param name="map">map of all the discovered devices</param>
        private void OnDiscovery(DeviceSDKErrorEventArgs e,
            Dictionary<CommsSettings, string> map)
        {
            try
            {
                if (e == null && map != null)
                {
                    _discoveredDeviceMap = map;

                    this.Dispatcher.BeginInvoke(new Action(
                        delegate ()
                        {
                            TreeViewItem ftie1000Node = (TreeViewItem)trvDevices.Items[0];
                            ftie1000Node.Items.Clear();
                            //map parameter has an entry for every discovered device
                            foreach (KeyValuePair<CommsSettings, string> cs in map)
                            {
                                TreeViewItem newDevice = new TreeViewItem();
                                //Take out the discovered IP Address and MAC address and add to the tree
                                newDevice.Header = (cs.Key as TCPCommsSettings).DeviceIPAddress + "-" + (cs.Key as TCPCommsSettings).DeviceMACAddress;
                                ftie1000Node.Items.Add(newDevice);
                            }
                        }
                        ));
                }
                else
                {
                    MessageBox.Show("Error Code :" + e.ErrorCode.ToString() + "\n\n\n\nMessage :" + e.ErrorMessage);
                }
            }
            catch (DeviceSDKException sdkEx)
            {
                MessageBox.Show("Error Code :" + sdkEx.ErrorEventArgs.ErrorCode.ToString() + "\n\n\n\nMessage :" + sdkEx.ErrorEventArgs.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n\n\nStack Trace :" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Live thread, receives packets and show them
        /// </summary>
        private void LiveThread()
        {
            while (true)
            {

                //Get the temperature Values and the processed frame values for the current frame 
                int[] _temperatureAndFrameValues = _SDKInstance.GetTemperatureAndFrameValues();
                if (_temperatureAndFrameValues != null)
                {
                    //Copy only the temperature values from the returned array.
                    Buffer.BlockCopy(_temperatureAndFrameValues, 0, _temperatureValues, 0, _videoWidth * _videoHeight * 4);

                    //Auto correct the temperature range for every frame.
                    _minimumTemperature = _SDKInstance.GetCurrentFrameMinimumTemperature();
                    _maximumTemperature = _SDKInstance.GetCurrentFrameMaximumTemperature();
                    _temperatureRange = _maximumTemperature - _minimumTemperature;


                    //Apply colour table to the temperature values returned from the camera, this results in a bitmap data array
                    for (int i = 0; i < _videoWidth * _videoHeight; i++)
                    {
                        int colourIndex = (int)((((_temperatureValues[i] / 10.0) - _minimumTemperature) / _temperatureRange) * _palette.ColourArray.Length + 0.5);

                        if (colourIndex < 0)
                            colourIndex = 0;
                        else if (colourIndex > 255)
                            colourIndex = 255;

                        _bitmapData[i] = _palette.ColourArray[colourIndex];
                    }

                    //Update the image control in user interface with the bitmap data
                    imgVideoPane.Dispatcher.Invoke(new Action<WriteableBitmap>((bitmap) =>
                    {
                        using (var context = bitmap.GetBitmapContext())
                        {
                            BitmapContext.BlockCopy(_bitmapData, 0, context, 0, _bitmapData.Length * 4);
                        }
                    }), _bmp);



                    Thread.Sleep(100);// 1 min ? 1s ?? 

                }
            }

            





        }
    }
}
