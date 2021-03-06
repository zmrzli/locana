﻿using Locana.CameraControl;
using Locana.Pages;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Locana.DataModel
{
    public class EntrancePanelGroupCollection : List<EntrancePanelGroup>, INotifyPropertyChanged, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        new public void Add(EntrancePanelGroup group)
        {
            base.Add(group);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, group, Count - 1));
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            try
            {
                CollectionChanged?.Invoke(this, e);
            }
            catch (NotSupportedException)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }

    public class EntrancePanelGroup : List<EntrancePanel>, INotifyPropertyChanged, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public EntrancePanelGroup(string name)
        {
            GroupKey = name;
        }

        public string GroupKey { private set; get; }

        new public void Add(EntrancePanel panel)
        {
            base.Add(panel);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, panel, Count - 1));
        }

        new public bool Remove(EntrancePanel content)
        {
            var index = IndexOf(content);
            var removed = base.Remove(content);
            if (removed)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, content, index));
            }
            return removed;
        }

        new public void Clear()
        {
            base.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnPropertyChanged(nameof(HasNoContent));
            try
            {
                CollectionChanged?.Invoke(this, e);
            }
            catch (NotSupportedException)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public bool HasNoContent => Count == 0;
    }

    public class EntrancePanel
    {
        public EntrancePanel(string name, string imageTemplateId, Action onClick)
        {
            PanelTitle = name;
            Transit = onClick;
            Resource = Application.Current.Resources[imageTemplateId] as DataTemplate;
        }

        public string PanelTitle { private set; get; }

        public DataTemplate Resource { private set; get; }

        public Action Transit { private set; get; }
    }

    public class DevicePanel : EntrancePanel
    {
        public DevicePanel(TargetDevice device)
            : base(device.FriendlyName,
                  "ic_linked_camera_white", () =>
             {
                 var shell = Window.Current.Content as AppShell;
                 var frame = shell.AppFrame as Frame;
                 frame.Navigate(typeof(ShootingPage), device);
             })
        {
            Device = device;
        }

        public TargetDevice Device
        {
            private set; get;
        }
    }

}
