// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using Midi;
using WebSocketSharp;
using WebSocket4Net;
using SocketIOClient;

namespace SkeletalTracking
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Channel leftChannel;
        Channel rightChannel;
        OutputDevice OutputDevice;
        List<NoteRectangle> rightNotes = new List<NoteRectangle>(12);
        List<NoteRectangle> leftNotes = new List<NoteRectangle>(12);
        Instrument leftInstrument = Instrument.AltoSax;
        Instrument rightInstrument = Instrument.OverdrivenGuitar;

        Note scaleNote = new Note('C');
        ScalePattern scalePattern = Scale.Major;

        Client socket;

        public MainWindow()
        {
            InitializeComponent();

            // startup our midi!
            OutputDevice = OutputDevice.InstalledDevices[0];
            
            OutputDevice.Open();
            //OutputDevice.SilenceAllNotes();
            leftChannel = Channel.Channel1;
            rightChannel = Channel.Channel2;

            // set instruments
            OutputDevice.SendProgramChange(leftChannel, leftInstrument);
            OutputDevice.SendProgramChange(rightChannel, rightInstrument);

            // draw a load of rectangles for the notes
            for (int i = 0; i < 12; i++)
            {
                // right hand
                NoteRectangle noteRectangle = new NoteRectangle()
                {
                    OutputDevice = OutputDevice,
                    Channel = rightChannel,
                    //Pitch = scale.NoteSequence[i % 7].PitchInOctave(3 + (i / 7 % 2)),
                    Rectangle = new Rectangle()
                };
                rightNotes.Add(noteRectangle);
                noteRectangle.Rectangle.Height = 40;
                noteRectangle.Rectangle.Width = 640;
                noteRectangle.Rectangle.StrokeThickness = i % 7 == 0 ? 4 : 1 ;
                noteRectangle.Rectangle.Stroke = i % 7 == 0 ? Brushes.White : Brushes.Gray;
                //noteRectangle.Rectangle.Fill = Brushes.Green;
                noteRectangle.Rectangle.Opacity = 0.4;
                //noteRectangle.Rectangle.Stroke.Opacity = 0.5;

                MainCanvas.Children.Add(noteRectangle.Rectangle);
                Canvas.SetLeft(noteRectangle.Rectangle, 0.0);
                Canvas.SetTop(noteRectangle.Rectangle, 480 - 40 - i * 40);
            }

            // initially just give it the scale of C
            scaleNote = new Note('C');
            scalePattern = Scale.Major;
            setScale();

            // connect to our node server
            socket = new Client("http://127.0.0.1:8080/kinect"); // url to nodejs 
            /*socket.Opened += SocketOpened;
            socket.Message += SocketMessage;
            socket.SocketConnectionClosed += SocketConnectionClosed;
            socket.Error += SocketError;*/

            
            
        }

        void setScale()
        {
            if (/*leftNotes.Count == 0 ||*/ rightNotes.Count == 0)
                return;

            Scale scale = new Scale(scaleNote, scalePattern);

            Dispatcher.Invoke(new Action(() => lblScaleNote.Content = scaleNote.ToString()));
            Dispatcher.Invoke(new Action(() => lblScaleMode.Content = scalePattern.Name));

            //lblScaleNote.Content = scaleNote.ToString();
            //lblScaleMode.Content = scalePattern.Name;

            for (int i = 0; i < 12; i++)
            {
                //leftNotes[i].Pitch = scale.NoteSequence[i % 7].PitchInOctave(3 + (i / 7 % 2));
                rightNotes[i].StopNote();
                //scale.NoteSequence
                if (i == 0)
                    rightNotes[i].Pitch = scale.NoteSequence[i % 7].PitchInOctave(3);
                else
                    rightNotes[i].Pitch = scale.NoteSequence[i % 7].PitchAtOrAbove(rightNotes[i-1].Pitch);
            }
            //scale.NoteSequence[0].pi
        }

        bool closing = false;
        const int skeletonCount = 6; 
        Skeleton[] allSkeletons = new Skeleton[skeletonCount];

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooser1.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);

            // register for 'connect' event with io server
            socket.On("connect", (fn) =>
            {
                //lblSocket.Content = "Connected";
                //lblSocket.Foreground = new SolidColorBrush(Colors.Green);
                Dispatcher.Invoke(new Action(() => lblSocket.Foreground = new SolidColorBrush(Colors.Green)));
                Dispatcher.Invoke(new Action(() => lblSocket.Content = "Connected"));
                //MessageBox.Show("connected");
            });

            // register for 'update' events - message is a json 'Part' object
            socket.On("keyscale", (data) =>
            {
                //MessageBox.Show(data.Json.ToJsonString());
                string note = data.Json.Args[0]["key"];
                string key = data.Json.Args[0]["scale"];

                scaleNote = new Note(note);
                switch (key)
                {
                    case "Chromatic": scalePattern = Scale.Chromatic; break;
                    case "HarmonicMinor": scalePattern = Scale.HarmonicMinor; break;
                    case "NaturalMinor": scalePattern = Scale.NaturalMinor; break;
                    case "Major": scalePattern = Scale.Major; break;
                }

                setScale();

                //MessageBox.Show(String.Format("Note: {0}, Tone: {1}", note, key));
                //Console.WriteLine("  raw message:      {0}", data.RawMessage);
                //Console.WriteLine("  string message:   {0}", data.MessageText);
                //Console.WriteLine("  json data string: {0}", data.Json.ToJsonString());
                //Console.WriteLine("  json raw:         {0}", data.Json.Args[0]);

                // cast message as Part - use type cast helper
                /*Part part = data.Json.GetFirstArgAs<Part>();
                Console.WriteLine(" Part Level:   {0}\r\n", part.Level);*/
            });

            // make the socket.io connection
            socket.Connect();
        }

        void kinectSensorChooser1_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            KinectSensor old = (KinectSensor)e.OldValue;

            StopKinect(old);

            KinectSensor sensor = (KinectSensor)e.NewValue;

            if (sensor == null)
            {
                return;
            }

            


            var parameters = new TransformSmoothParameters
            {
                Smoothing = 0.4f,
                Correction = 0.2f,
                Prediction = 0.5f,
                JitterRadius = 3.0f,
                MaxDeviationRadius = 0.5f
            };
            sensor.SkeletonStream.Enable(parameters);

            sensor.SkeletonStream.Enable();

            sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(sensor_AllFramesReady);
            sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30); 
            sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

            try
            {
                sensor.Start();
            }
            catch (System.IO.IOException)
            {
                kinectSensorChooser1.AppConflictOccurred();
            }
        }

        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (closing)
            {
                return;
            }

            //Get a skeleton
            Skeleton first =  GetFirstSkeleton(e);

            if (first == null)
            {
                return; 
            }



            //set scaled position
            //ScalePosition(headImage, first.Joints[JointType.Head]);
            ScalePosition(leftEllipse, first.Joints[JointType.HandLeft]);
            ScalePosition(rightEllipse, first.Joints[JointType.HandRight]);

            GetCameraPoint(first, e); 

        }

        

        void GetCameraPoint(Skeleton first, AllFramesReadyEventArgs e)
        {

            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (depth == null ||
                    kinectSensorChooser1.Kinect == null)
                {
                    return;
                }
                

                //Map a joint location to a point on the depth map
                //head
                /*DepthImagePoint headDepthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.Head].Position);*/
                //left hand
                DepthImagePoint leftDepthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.HandLeft].Position);
                //right hand
                DepthImagePoint rightDepthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.HandRight].Position);


                //Map a depth point to a point on the color image
                //head
                /*ColorImagePoint headColorPoint =
                    depth.MapToColorImagePoint(headDepthPoint.X, headDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);*/
                //left hand
                ColorImagePoint leftColorPoint =
                    depth.MapToColorImagePoint(leftDepthPoint.X, leftDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);
                //right hand
                ColorImagePoint rightColorPoint =
                    depth.MapToColorImagePoint(rightDepthPoint.X, rightDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);

                

                //Set location
                //CameraPosition(headImage, headColorPoint);
                CameraPosition(leftEllipse, leftColorPoint);
                CameraPosition(rightEllipse, rightColorPoint);

                // do the points
                bool leftValid = false; bool rightValid = false;
                leftEllipse = doDepthCalculation(leftEllipse, leftDepthPoint, 1250, true, out leftValid);
                rightEllipse = doDepthCalculation(rightEllipse, rightDepthPoint, 1250, false, out rightValid);

                // play / turn off the right notes
                // right hand
                if (rightValid)
                {
                    int rectangleIndex = -1;
                    NoteRectangle thisNoteRectangle = getNoteRectangleAtPoint(rightColorPoint, false, out rectangleIndex);
                    thisNoteRectangle.PlayNote();
                    thisNoteRectangle.Rectangle.Fill = Brushes.Green;
                    rightHandLabel.Content = thisNoteRectangle.Pitch.ToString();
                    for (int x = 0; x < rightNotes.Count; x++)
                    {
                        if (x != rectangleIndex)
                        {
                            rightNotes[x].StopNote();
                            rightNotes[x].Rectangle.Fill = null;
                        }
                    }
                }
                else
                {
                    foreach (NoteRectangle x in rightNotes)
                    {
                        x.StopNote();
                    }
                    int rectangleIndex = -1;
                    getNoteRectangleAtPoint(rightColorPoint, false, out rectangleIndex).StopNote();
                }

            }        
        }


        Ellipse doDepthCalculation(Ellipse ellipse, DepthImagePoint depthImagePoint, int planeDepthInMilimeters, bool leftHand, out bool valid)
        {
            if (depthImagePoint.Depth > planeDepthInMilimeters)
            {
                // no, not valid :(
                // invalid
                valid = false;

                // mark as red, dont do shit, transparencize to help user
                ellipse.Fill = new SolidColorBrush(Colors.Red);
                if (depthImagePoint.Depth < planeDepthInMilimeters + 50)
                {
                    ellipse.Fill = new SolidColorBrush(Colors.HotPink);
                }
            }
            else
            {
                valid = true;

                // colour green
                ellipse.Fill = new SolidColorBrush(Colors.Green);
            }

            // set the transparency to the distance away from our plane
            int maxDistanceFromPlane = planeDepthInMilimeters + 500;
            double opacity = 0.0;
                
            int normalisedMaxDistance = maxDistanceFromPlane - planeDepthInMilimeters;
            int normalisedPointDepth = depthImagePoint.Depth - planeDepthInMilimeters;
            double ratio;
            if (depthImagePoint.Depth < maxDistanceFromPlane)
                ratio = 1d - (double)normalisedPointDepth / normalisedMaxDistance;
            else
                ratio = 1d - (double)normalisedPointDepth / normalisedMaxDistance;
                opacity = ratio;
                
            ellipse.Opacity = opacity;

            return ellipse;
        }

        NoteRectangle getNoteRectangleAtPoint(ColorImagePoint colorImagePoint, bool leftHand, out int index)
        {
            int noteInOctave = (int)((double)(480 - colorImagePoint.Y + 50) / 480 * 12) - 1;
            if (leftHand)
            {
                index = noteInOctave;
                return leftNotes[noteInOctave];
            }
            else
            {
                index = noteInOctave;

                if (index < 0)
                    index = 0;
                if (index > 11)
                    index = 11;

                return rightNotes[index];
            }
        }


        Skeleton GetFirstSkeleton(AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if (skeletonFrameData == null)
                {
                    return null; 
                }

                
                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                //get the first tracked skeleton
                Skeleton first = (from s in allSkeletons
                                         where s.TrackingState == SkeletonTrackingState.Tracked
                                         select s).FirstOrDefault();

                return first;

            }
        }

        private void StopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                if (sensor.IsRunning)
                {
                    //stop sensor 
                    sensor.Stop();

                    //stop audio if not null
                    if (sensor.AudioSource != null)
                    {
                        sensor.AudioSource.Stop();
                    }


                }
            }
        }

        private void CameraPosition(FrameworkElement element, ColorImagePoint point)
        {
            //Divide by 2 for width and height so point is right in the middle 
            // instead of in top/left corner
            Canvas.SetLeft(element, point.X - element.Width / 2);
            Canvas.SetTop(element, point.Y - element.Height / 2);

        }


        private void ScalePosition(FrameworkElement element, Joint joint)
        {
            //convert the value to X/Y
            //Joint scaledJoint = joint.ScaleTo(1280, 720); 
            
            //convert & scale (.3 = means 1/3 of joint distance)
            Joint scaledJoint = joint.ScaleTo(640, 480, .3f, .3f);

            Canvas.SetLeft(element, scaledJoint.Position.X);
            Canvas.SetTop(element, scaledJoint.Position.Y); 
            
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true; 
            StopKinect(kinectSensorChooser1.Kinect); 
        }

        private void cmbScaleMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object item = ((ComboBox)sender).SelectedItem;
            ComboBoxItem cmbitem = (ComboBoxItem)item;
            
            switch (cmbitem.Content.ToString())
            {
                case "Chromatic" : scalePattern = Scale.Chromatic; break;
                case "HarmonicMinor" : scalePattern = Scale.HarmonicMinor; break;
                case "NaturalMinor" : scalePattern = Scale.NaturalMinor; break;
                case "Major" : scalePattern = Scale.Major; break;
            }

            setScale();
        }

        private void cmbScaleNote_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object item = ((ComboBox)sender).SelectedItem;
            ComboBoxItem cmbitem = (ComboBoxItem)item;

            scaleNote = new Note(cmbitem.Content.ToString());

            setScale();
        }

        
    }
}
