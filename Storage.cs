using System;
using System.IO;
using System.Runtime.Serialization.Json;

public sealed class GardenLoadResult {
 public SaveData Data;
 public string Notice;
 public bool ReadOnly;
}

public static class GardenStorage {
 const long MaxBytes=5000000;
 public static SaveData Read(string path) {
  if(new FileInfo(path).Length>MaxBytes) throw new InvalidDataException("The garden file exceeds 5 MB.");
  using(var file=File.OpenRead(path)) { var data=(SaveData)new DataContractJsonSerializer(typeof(SaveData)).ReadObject(file); Garden.Validate(data); return data; }
 }
 public static GardenLoadResult Load(string path) {
  if(File.Exists(path)) { try { return new GardenLoadResult { Data=Read(path) }; } catch { } }
  else if(!File.Exists(path+".bak")) return new GardenLoadResult { Data=new SaveData() };
  if(File.Exists(path+".bak")) {
   try {
    var restored=Read(path+".bak"); bool canSave=Preserve(path);
    return new GardenLoadResult { Data=restored,ReadOnly=!canSave,Notice=canSave?"Recovered your garden from its previous save. The unreadable file was kept as a recovery copy.":"Recovered your previous save, but the original file cannot be moved. Automatic saving is disabled. Export your garden to a writable folder." };
   } catch { }
  }
  bool preserved=Preserve(path); preserved=Preserve(path+".bak") && preserved;
  return new GardenLoadResult { Data=new SaveData(),ReadOnly=!preserved,Notice=preserved?"The garden could not be read. The original files were kept as recovery copies. You can import another backup.":"The garden could not be read and its files cannot be moved. Automatic saving is disabled. Choose Export garden to save elsewhere." };
 }
 static bool Preserve(string path) {
  if(!File.Exists(path)) return true;
  try { File.Move(path,path+".recovery-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)); return true; } catch { return false; }
 }
 public static void Write(string path,SaveData data) {
  Garden.Validate(data); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
  string temporary=path+".tmp";
  try {
   using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)) { new DataContractJsonSerializer(typeof(SaveData)).WriteObject(file,data); if(file.Length>MaxBytes) throw new InvalidDataException("The garden exceeds the 5 MB storage limit. Export your data before adding more."); file.Flush(true); }
   if(File.Exists(path)) File.Replace(temporary,path,path+".bak"); else File.Move(temporary,path);
  } finally { if(File.Exists(temporary)) File.Delete(temporary); }
 }
}
