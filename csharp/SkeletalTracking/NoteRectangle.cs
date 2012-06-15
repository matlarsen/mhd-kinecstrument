using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Shapes;
using Midi;
using System.Windows.Media;

namespace SkeletalTracking
{
    public class NoteRectangle
    {
        public Rectangle Rectangle {get; set;}
        public Pitch Pitch {get; set;}
        public OutputDevice OutputDevice {get; set;}
        public Channel Channel {get; set;}
        public bool playing = false;

        public void PlayNote()
        {
            if (!playing)
            {
                OutputDevice.SendNoteOn(Channel, Pitch, 80);
                playing = true;
                //Rectangle.Fill = new SolidColorBrush(Colors.Green);
                Rectangle.Opacity = 0.3;
            }
        }

        public void StopNote()
        {
            if (playing)
            {
                OutputDevice.SendNoteOff(Channel, Pitch, 80);
                playing = false;
                //Rectangle.Opacity = 0;
            }
        }
    }
}
