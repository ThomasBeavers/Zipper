using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ICSharpCode.SharpZipLib.Zip;
using ManyConsole.CommandLineUtils;

namespace Zipper
{
	public class MultiDirCommand : ConsoleCommand
	{
		private DirectoryInfo Source { get; set; }
		private DirectoryInfo Output { get; set; }

		private SubjectDictionary<string, string> SubEvents { get; set; }
		private SubscriptionCollection Subscriptions { get; set; }
		
		public MultiDirCommand()
		{
			IsCommand("multi", "Watches for file changes and maintains zips for each sub-directory");

			this.HasOption("o|output=", "Output directory to place zips. (Defaults to Source/../Zips)", o => Output = new DirectoryInfo(o));
			this.HasAdditionalArguments(1, "Source directory to search for sub-directories");
		}

		public override int Run(string[] remainingArguments)
		{
			Source = new DirectoryInfo(remainingArguments[0]);

			if (Output == null)
			{
				Output = new DirectoryInfo(Path.Combine(Source.Parent.FullName, "Zips"));
			}

			if (Output.Exists)
			{
				Output.Delete(true);
			}
			Output.Create();

			using(SubEvents = new SubjectDictionary<string, string>())
			{
				using(Subscriptions = new SubscriptionCollection())
				{
					Source.EnumerateDirectories()
						.Where(child => !child.Attributes.HasFlag(FileAttributes.Hidden))
						.ForEach(ChildChanged);

					// Create a new FileSystemWatcher and set its properties.
					using (FileSystemWatcher watcher = new FileSystemWatcher())
					{
						watcher.Path = Source.FullName;
						watcher.IncludeSubdirectories = true;

						// Watch for changes in LastAccess and LastWrite times, and
						// the renaming of files or directories.
						watcher.NotifyFilter = NotifyFilters.LastAccess
											| NotifyFilters.LastWrite
											| NotifyFilters.FileName
											| NotifyFilters.DirectoryName;

						// Add event handlers.
						watcher.Changed += OnChanged;
						watcher.Created += OnChanged;
						watcher.Deleted += OnChanged;
						watcher.Renamed += OnChanged;

						// Begin watching.
						watcher.EnableRaisingEvents = true;

						// Wait for the user to quit the program.
						Console.WriteLine("Press 'q' to quit the sample.");
						while (Console.Read() != 'q') ;
					}
				}
			}

			return 0;
		}

		private void ChildChanged(DirectoryInfo di)
		{
			var fullName = di.FullName;

			if ( !SubEvents.ContainsKey(fullName) )
			{
				var subject = new Subject<string>();
				SubEvents.Add(fullName, subject);

				Subscriptions.Add(subject
					.Throttle(TimeSpan.FromSeconds(1))
					.SubscribeWithoutOverlap(OnThrottled));
			}

			SubEvents[fullName].OnNext(fullName);
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{	
			try
			{
				var path = e.FullPath;

				// get the file attributes for file or directory
				FileAttributes attr = File.GetAttributes(path);
				if (attr.HasFlag(FileAttributes.Hidden))
					return;

				var di = attr.HasFlag(FileAttributes.Directory)
					? new DirectoryInfo(path)
					: new FileInfo(path).Directory;

				if (Source.FullName == di.FullName)
					return;

				while(Source.FullName != di.Parent.FullName)
				{
					di = di.Parent;
				}

				if (di.Attributes.HasFlag(FileAttributes.Hidden))
					return;

				ChildChanged(di);
			}
			catch(FileNotFoundException)
			{
				// intentionally left blank
			}
		}

		private void OnThrottled(string path)
		{
			var di = new DirectoryInfo(path);

			Console.WriteLine("Zipping: " + di.Name);

			var fiTarget = new FileInfo(Path.Combine(Output.FullName, di.Name + ".zip"));

			if(fiTarget.Exists)
				fiTarget.Delete();

			var fz = new FastZip();
			fz.CreateZip(fiTarget.FullName, di.FullName, true, null);
		}
	}
}