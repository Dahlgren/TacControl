// /*
// * Copyright (C) Dedmen Miller @ R4P3 - All Rights Reserved
// * Unauthorized copying of this file, via any medium is strictly prohibited
// * Proprietary and confidential
// * Written by Dedmen Miller <dedmen@dedmen.de>, 08 2016
// */

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TacControl.Common.Modules
{

    public class GPSTracker : INotifyPropertyChanged
    {
        public string id { get; set; }
        //[JsonConverter(typeof(DenseVectorConverter))]
        //public Vector3 pos { get; set; }
        //[JsonConverter(typeof(DenseVectorConverter))]
        //public Vector3 vel { get; set; }
        public string displayName { get; set; }

        public ObservableCollection<float> pos { get; set; } = new ObservableCollection<float> { };
        public ObservableCollection<float> vel { get; set; } = new ObservableCollection<float> { };

        GPSTracker()
        {
            pos.CollectionChanged += (a, e) =>
            {
                if (pos.Count > 3) //#TODO this is stupid, need Vector3 support
                    pos.RemoveAt(0);
                OnPropertyChanged("pos");
            };
            vel.CollectionChanged += (a, e) =>
            {
                if (vel.Count > 3)
                    vel.RemoveAt(0);
                OnPropertyChanged("vel");
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ModuleGPS : INotifyPropertyChanged
    {
        public ObservableDictionary<string, GPSTracker> trackers { get; set; } = new ObservableDictionary<string, GPSTracker>();


        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetTrackerName(GPSTracker tracker, string descriptionText)
        {
            Networking.Instance.SendMessage(

                $@"{{
                    ""cmd"": [""GPS"", ""SetTrackerName""],
                    ""args"": {{
                        ""tracker"": ""{tracker.id}"",
                        ""name"": ""{descriptionText}""
                    }}
                }}"
            );
        }
    }
}
