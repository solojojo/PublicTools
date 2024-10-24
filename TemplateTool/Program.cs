using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using TemplateTool;
using System.Text.Json;
using System.IO;


const ConsoleColor ParamColor = ConsoleColor.Yellow;
const ConsoleColor NormalText = ConsoleColor.White;

ConsoleColor FormattedLineDefaultColor = NormalText;

void WriteFormattedLine(string format, params string[] answers )
{
	Console.ForegroundColor = FormattedLineDefaultColor;
	int formatLength = format.Length;
	int currIndex = 0;
	bool readingNumber = false;
	string numberRead = string.Empty;
	while (currIndex < formatLength)
	{
		var ch = format[currIndex];
		switch (ch)
		{
			case '{':
				Console.ForegroundColor = ParamColor;
				readingNumber = true;
				numberRead = string.Empty;
				break;
			case '}':
				var number = int.Parse(numberRead);
				var answer = answers[number];
				Console.Write(answer);
				Console.ResetColor();
				Console.ForegroundColor = FormattedLineDefaultColor;
				readingNumber = false;
				break;
			default:
				if (readingNumber)
					numberRead += ch;
				else
					Console.Write(ch);
				break;
		}

		currIndex++;
	}
	Console.WriteLine();
	Console.ResetColor();
}



// read paramters from a json file 
// parse the file and replace any substring with the new plugin name
// get the current working directory
String CurrentDirectory = Directory.GetCurrentDirectory();

String ConfigFile = CurrentDirectory+"\\Config.json";

if ( !File.Exists(ConfigFile) )
{
	FormattedLineDefaultColor = ConsoleColor.Red;
	WriteFormattedLine("Failed to find `{0}`!\n", ConfigFile);
	return;
}
String ConfigJSon = File.ReadAllText(ConfigFile);
ConfigParams? Config = JsonSerializer.Deserialize<ConfigParams>(ConfigJSon);

if (Config == null
	|| Config.RootFolder == null
	|| Config.SubFolder == null
	|| Config.TemplatePlugin == null
	|| Config.TemplateDir == null
	)
{
	FormattedLineDefaultColor = ConsoleColor.Red;
	WriteFormattedLine("Failed to parse `{0}`!\n", ConfigFile);
	return;
}


void FatalError(String Message)
{
	const ConsoleColor ErrorColor = ConsoleColor.Red;
	const ConsoleColor InfoColor = ConsoleColor.Green;
	FormattedLineDefaultColor = InfoColor;

	Console.ForegroundColor = ErrorColor;
	Console.WriteLine(Message);
	Console.ForegroundColor = NormalText;
	Console.WriteLine("Usage:");


	//////////////////////////////////////////////////
	/// new plugin

	Console.ForegroundColor = NormalText;
	Console.WriteLine("\tTemplateTool -command new NewPlugin");
	Console.ForegroundColor = InfoColor;

	WriteFormattedLine("Clones the template project from '{0}\\{1}' to the '{2}' subfolder.", Config.TemplateDir, Config.TemplatePlugin, Config.SubFolder);
	Console.WriteLine("The folder will be deleted completely - so make sure to use UPDATE for existing plugins.\n");


	//////////////////////////////////////////////////
	/// update plugin

	Console.ForegroundColor = NormalText;
	Console.WriteLine("\tTemplateTool -command update NewPlugin");
	Console.ForegroundColor = InfoColor;
	WriteFormattedLine("Updates the specified plugin from '{0}\\{1}'.\n", Config.TemplateDir, Config.TemplatePlugin );


	//////////////////////////////////////////////////
	/// merge plugin

	Console.ForegroundColor = NormalText;
	Console.WriteLine("\tTemplateTool -command merge Plugin FilesSubstring");
	Console.ForegroundColor = InfoColor;
	WriteFormattedLine("For any files that exist on the target, make a temporary file with a newly cloned update'.");
	WriteFormattedLine("This is useful for manually merging in any changes from the template plugin.\n");
	WriteFormattedLine("If FilesSubstring is '{0}' then all files will be copied.\n", "*");

	Console.Write("Any new files be added, but existing files will be left untouched.\n");


	Console.ForegroundColor = NormalText;
	// exit the program
	Environment.Exit(1);
}

// this function gathers all of the files in the given folder
String[] GatherFiles(String SourceDir, bool SkipIntermediate)
{
	// get all of the files in the given 
	String[] Files = Directory.GetFiles(SourceDir, "*.*", SearchOption.AllDirectories);

	if (SkipIntermediate)
	{
		// filter out any files that's in a BINARIES or INTERMEDIATE folder
		Files = Files.Where(x => !x.Contains("Binaries") && !x.Contains("Intermediate")).ToArray();
	}
	return Files;
}

// get the command line arguments
string[] Args = Environment.GetCommandLineArgs();

// trim the current directory to the root folder
String RootDir = CurrentDirectory.Substring(0, CurrentDirectory.IndexOf(Config.RootFolder) + Config.RootFolder.Length);

// scan the command line arguments for the command to execute
// find the "-command" argument
int CommandIndex = Array.IndexOf(Args, "-command");
if (CommandIndex == -1)
{
	FatalError("Failed to find `-command` parameter!\n");
}
++CommandIndex;

if (Args.Length <= CommandIndex+1)
{
	FatalError("Invalid number of arguments!\n");
}

String Command = Args[CommandIndex].ToLower();
// renames the substring in TemplatePlugin to our new plugin name in the file list
String[] RenameFiles(String[] Files, String NewPlugin)
{
	// create a new string array
	String[] NewFiles = new String[Files.Length];
	// loop through all of the files
	for (int i = 0; i < Files.Length; i++)
	{
		NewFiles[ i ] = Files[i].Replace(Config.TemplatePlugin, NewPlugin );
		NewFiles[ i ] = NewFiles[i].Replace("Templates\\", Config.SubFolder +"\\");


	}
	return NewFiles;
}

++CommandIndex;
String NewPluginName = Args[CommandIndex];

String AbsSourceTemplateDir = RootDir + "\\" + Config.TemplateDir + "\\" + Config.TemplatePlugin;
String[] TemplateFiles = GatherFiles(AbsSourceTemplateDir, true );
String[] TargetFiles = RenameFiles(TemplateFiles, NewPluginName);
String TargetDir = RootDir + "\\" + Config.SubFolder + "\\" + NewPluginName;

int[] FilesToCopy = new int[0];
// assert that the template and target lists are the same length
if (TemplateFiles.Length != TargetFiles.Length)
{
	FatalError("Internal error! Template and target file lists are not the same length!\n");
}

if ( Command == "new" )
{
	WriteFormattedLine("Generating new plugin {0} from `{1}`", NewPluginName, AbsSourceTemplateDir);

	// we want to copy all the files, and first remove the directory if it exists

	// remove the target folder if it exists
	if (Directory.Exists(TargetDir))
	{
		String[] FilesToDelete = GatherFiles(TargetDir, false);
		// delete the files first
		foreach (String FileToDelete in FilesToDelete)
		{
			File.SetAttributes(FileToDelete, File.GetAttributes(FileToDelete) & ~FileAttributes.ReadOnly);
			File.Delete(FileToDelete);
		}

		Directory.Delete(TargetDir, true);
	}

	// add all our files to the copy list
	FilesToCopy = Enumerable.Range(0, TemplateFiles.Length).ToArray();
	Console.WriteLine("Found {0} files to copy", FilesToCopy.Length);
}
else if (Command == "update")
{
	WriteFormattedLine("Updating existing plugin {0} from `{1}`", NewPluginName, AbsSourceTemplateDir);

	String[] ExistingFiles = GatherFiles(TargetDir, true);
	// only add files we haven't copied yet
	FilesToCopy = Enumerable.Range(0, TemplateFiles.Length).Where(x => !ExistingFiles.Contains(TargetFiles[x])).ToArray();

	WriteFormattedLine("Found {0} new files to update", FilesToCopy.Length.ToString() );

}
else if (Command == "merge")
{

	++CommandIndex;
	if (Args.Length <= CommandIndex)
	{
		FatalError("Invalid number of arguments for Merge - missing argument for file substring !\n");
	}
	String FileSubstring = Args[CommandIndex];
	String[] ExistingFiles = GatherFiles(TargetDir, true);

	// filter the files based on our substring
	FilesToCopy = Enumerable.Range(0, TemplateFiles.Length).Where(x => FileSubstring == "*" || TemplateFiles[x].ToLower().Contains(FileSubstring.ToLower())).ToArray();

	// add a ".MERGE" to the end of all of the target filenames

	for ( int i = 0; i < TargetFiles.Length; ++i )
	{
		TargetFiles[ i ] += ".MERGE";
	}


}
else 
{
	// format a string
	FatalError(String.Format("Invalid command {0}!\n", Command));
}

string TempAPI = "REPLACEME_API";
string SourcePluginAPI = (Config.TemplatePlugin + "_API").ToUpper();
string TargetPluginAPI = (NewPluginName + "_API").ToUpper();
// copy all of our targeted files
foreach (int i in FilesToCopy)
{
	String SourceFile = TemplateFiles[i];
	String TargetFile = TargetFiles[i];

	WriteFormattedLine("Copying {0} to {1}", SourceFile, TargetFile);

	// create the entire folder hierarchy for the target file
	string? DirPath = Path.GetDirectoryName(TargetFile);
	if (DirPath != null )
	{
		Directory.CreateDirectory(DirPath);
	}

	// strip the read only flag from the target
	if (File.Exists(TargetFile))
	{
		File.SetAttributes(TargetFile, File.GetAttributes(TargetFile) & ~FileAttributes.ReadOnly);
	}
	// parse the file and replace any substring with the new plugin name
	String FileContents = File.ReadAllText(SourceFile);


	FileContents = FileContents.Replace(SourcePluginAPI, TempAPI);
	FileContents = FileContents.Replace(Config.TemplatePlugin, NewPluginName);
	FileContents = FileContents.Replace(TempAPI, TargetPluginAPI);

	// write the file contents to the target file
	File.WriteAllText(TargetFile, FileContents);

}
