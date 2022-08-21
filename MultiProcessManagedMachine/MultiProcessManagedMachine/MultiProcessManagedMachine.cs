using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;
using MultiProcessManagedMachine;

namespace WDE.MPMM
{
    [MachineDecl(Name = "Multi Process Managed Machine", ShortName = "MPMM", Author = "WDE", MaxTracks = 8)]
	public class MultiProcessManagedMachine : IBuzzMachine, INotifyPropertyChanged, IDisposable
	{
		IBuzzMachineHost host;
		ProcessHost processHost;
		public static readonly string PIPE_NAME = "MultiProcessManagedMachinePIPE";
		public static int UniqueID = 1;

		public MultiProcessManagedMachine(IBuzzMachineHost host)
		{
			this.host = host;
			NoiseGain = new Interpolator();

			processHost = new ProcessHost();
			processHost.Start(PIPE_NAME + UniqueID++);
			processHost.Send(Protocol.Command.Ping);
			if (processHost.Read(Protocol.Command.Pong))
			{
				Global.Buzz.DCWriteLine("We have IPC connection!");
			}
			Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;
		}

        private void Song_MachineRemoved(IMachine obj)
        {
			if (host.Machine == obj)
				this.Dispose();
        }

        [ParameterDecl(ResponseTime = 5, MaxValue = 127, DefValue = 80, Transformation = Transformations.Cubic, TransformUnityValue = 80, ValueDescriptor = Descriptors.Decibel)]
		public Interpolator NoiseGain { get; private set; }

		[ParameterDecl(ValueDescriptions = new[] { "no", "yes" })]
		public bool Bypass { get; set; }


		[ParameterDecl(MaxValue = 127, DefValue = 0)]
		public void ATrackParam(int v, int track)
		{
			// track parameter example
		}

		public bool Work(Sample[] output, Sample[] input, int n, WorkModes mode)
		{
			if (mode == WorkModes.WM_WRITE)
				return false;

			if (!Bypass)
            {
				Sample[] samples = new Sample[n];
				processHost.RequestSamples(samples);
				for (int i = 0; i < n; i++)
				{
					output[i] = input[i] + samples[i] * 5000 * NoiseGain.Tick();
				}
				return true;
			}
			else
            {
				for (int i = 0; i < n; i++)
				{
					output[i] = input[i];
				}
				return true;
			}
		}

        public void Dispose()
        {
			processHost.Dispose();
		}

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class State : INotifyPropertyChanged
		{
			public State() { text = "here is state"; }	// NOTE: parameterless constructor is required by the xml serializer

			string text;
			public string Text 
			{
				get { return text; }
				set
				{
					text = value;
					if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Text"));
					// NOTE: the INotifyPropertyChanged stuff is only used for data binding in the GUI in this demo. it is not required by the serializer.
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;
		}

		State machineState = new State();
		public State MachineState			// a property called 'MachineState' gets automatically saved in songs and presets
		{
			get { return machineState; }
			set
			{
				machineState = value;
				if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
			}
		}		
		
		int checkedItem = 1;
		
		public IEnumerable<IMenuItem> Commands
		{
			get
			{
				yield return new MenuItemVM() { Text = "Hello" };
				yield return new MenuItemVM() { IsSeparator = true };
				yield return new MenuItemVM()
				{
					Text = "Submenu",
					Children = new[]
					{
						new MenuItemVM() { Text = "Child 1" },
						new MenuItemVM() { Text = "Child 2" }
					}
				};
				yield return new MenuItemVM() { Text = "Label", IsLabel = true };
				yield return new MenuItemVM()
				{
					Text = "Checkable",
					Children = new[]
					{
						new MenuItemVM() { Text = "Child 1", IsCheckable = true, StaysOpenOnClick = true },
						new MenuItemVM() { Text = "Child 2", IsCheckable = true, StaysOpenOnClick = true },
						new MenuItemVM() { Text = "Child 3", IsCheckable = true, StaysOpenOnClick = true }
					}
				};

				var g = new MenuItemVM.Group();
				
				yield return new MenuItemVM()
				{
					Text = "CheckGroup",
					Children = Enumerable.Range(1, 5).Select(i => new MenuItemVM() 
					{ 
						Text = "Child " + i, 
						IsCheckable = true, 
						CheckGroup = g,
						StaysOpenOnClick = true, 
						IsChecked = i == checkedItem, 
						CommandParameter = i,
						Command = new SimpleCommand()
						{
							CanExecuteDelegate = p => true,
							ExecuteDelegate = p => checkedItem = (int)p
						}
					})
				};

				yield return new MenuItemVM() 
				{ 
					Text = "About...", 
					Command = new SimpleCommand()
					{
						CanExecuteDelegate = p => true,
						ExecuteDelegate = p => MessageBox.Show("About")
					}
				};
			}
		}		

		public event PropertyChangedEventHandler PropertyChanged;
	}

	public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new MultiProcessManagedMachineGUI(); } }
	public class MultiProcessManagedMachineGUI : UserControl, IMachineGUI
	{
		IMachine machine;
		MultiProcessManagedMachine gainMachine;
		TextBox tb;
		ListBox lb;

		// view model for machine list box items
		public class MachineVM
		{
			public IMachine Machine { get; private set; }
			public MachineVM(IMachine m) { Machine = m; }
			public override string ToString() { return Machine.Name; }
		}
		
		public ObservableCollection<MachineVM> Machines { get; private set; }
		
		public IMachine Machine
		{
			get { return machine; }
			set
			{
				if (machine != null)
				{
					BindingOperations.ClearBinding(tb, TextBox.TextProperty);
					machine.Graph.MachineAdded -= machine_Graph_MachineAdded;
					machine.Graph.MachineRemoved -= machine_Graph_MachineRemoved;
				}

				machine = value;

				if (machine != null)
				{
					gainMachine = (MultiProcessManagedMachine)machine.ManagedMachine;
					tb.SetBinding(TextBox.TextProperty, new Binding("MachineState.Text") { Source = gainMachine, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
					
					machine.Graph.MachineAdded += machine_Graph_MachineAdded;
					machine.Graph.MachineRemoved += machine_Graph_MachineRemoved;
					
					foreach (var m in machine.Graph.Machines)
						machine_Graph_MachineAdded(m);
					
					lb.SetBinding(ListBox.ItemsSourceProperty, new Binding("Machines") { Source = this, Mode = BindingMode.OneWay });
				}
			}
		}

		void machine_Graph_MachineAdded(IMachine machine)
		{
			Machines.Add(new MachineVM(machine));
		}

		void machine_Graph_MachineRemoved(IMachine machine)
		{
			Machines.Remove(Machines.First(m => m.Machine == machine));
		}
		
		public MultiProcessManagedMachineGUI()
		{
			Machines = new ObservableCollection<MachineVM>();
		
			tb = new TextBox() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = true };
			lb = new ListBox() { Height = 100, Margin = new Thickness(0, 0, 0, 4) };

			var sp = new StackPanel();
			sp.Children.Add(tb);
			sp.Children.Add(lb);
			this.Content = sp;
			
			// drag and drop example
			tb.PreviewDragEnter += (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
			tb.PreviewDragOver += (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
			tb.Drop += (sender, e) => { tb.Text = "drop"; e.Handled = true; };			
		}

	}

}
