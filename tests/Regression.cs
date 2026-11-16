using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class Regression {
 static int passed;
 static void Check(bool condition,string description) { if(!condition) throw new Exception(description); passed++; }
 static void Reject(Action action,string description) { try { action(); } catch { passed++; return; } throw new Exception(description); }
 [STAThread] public static int Main(string[] args) {
  try {
   Garden.Main(new[]{"--self-test"});
   CheckTasks(); CheckWeek(); CheckMerging(); CheckStorage();
   System.Windows.Forms.Application.EnableVisualStyles(); System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
   CheckKeyboard();
   Console.WriteLine("PASS: "+passed+" additional regression assertions");
   string preview=Path.Combine(Environment.CurrentDirectory,"work","test-preview");
   Garden.RenderPreview(preview);
   Console.WriteLine("PASS: off-screen UI, presence lifecycle and preview checks");
   return 0;
  } catch(Exception error) { Console.Error.WriteLine("FAIL: "+error); return 1; }
 }
 static void CheckTasks() {
  var data=new SaveData(); DateTime now=DateTime.UtcNow;
  var first=TaskStore.Add(data,"  Write tests  ",now); var second=TaskStore.Add(data,"Review code",now.AddSeconds(1));
  Check(first.Title=="Write tests" && first.Id!=second.Id,"Task trim and IDs");
  Reject(delegate { TaskStore.Add(data," ",now); },"Accepted blank task");
  Reject(delegate { TaskStore.Add(data,new string('x',121),now); },"Accepted oversized task");
  Check(TaskStore.Select(data,first.Id) && data.ActiveTaskId==first.Id && data.Task==first.Title,"Task selection did not synchronize");
  TaskStore.RenameActive(data,"Write regression tests"); Check(first.Title=="Write regression tests","Selected task rename");
  data.Running=true;
  Check(!TaskStore.Select(data,second.Id),"Switched task during a running session");
  Check(!TaskStore.ToggleDone(data,first.Id,now),"Completed active running task");
  Check(!TaskStore.Archive(data,first.Id),"Archived active running task");
  Check(TaskStore.ToggleDone(data,second.Id,now) && second.Done,"Unrelated task completion");
  data.Running=false;
  Check(TaskStore.ToggleDone(data,first.Id,now) && first.CompletedAt.HasValue && data.ActiveTaskId=="","Task completion did not clear selection");
  TaskStore.ArchiveDone(data); Check(data.Tasks.All(t=>t.Archived) && data.Tasks.Count==2,"Archive should retain records");
  Check(!TaskStore.Select(data,first.Id),"Selected archived task");
  var third=TaskStore.Add(data,"Read docs",now); TaskStore.ToggleDone(data,third.Id,now); TaskStore.ToggleDone(data,third.Id,now);
  Check(!third.Done && !third.CompletedAt.HasValue,"Reopen should clear completion date");
  TaskStore.Select(data,third.Id); TaskStore.RenameActive(data,""); Check(data.ActiveTaskId=="" && third.Title=="Read docs","Empty editor should detach rather than erase task");
  Check(TaskStore.Archive(data,third.Id) && !third.Done,"Archive pending task without faking completion");
  Check(TaskStore.Restore(data,third.Id) && !third.Archived,"Restore an archived task"); TaskStore.Archive(data,third.Id);
  for(int i=0;i<200;i++) TaskStore.Add(data,"Task "+i,now);
  Reject(delegate { TaskStore.Add(data,"Overflow",now); },"Task queue limit not enforced");
  Check(!TaskStore.Restore(data,third.Id) && third.Archived,"Restore should respect the visible queue limit");
 }
 static void CheckWeek() {
  DateTime today=DateTime.Today; var data=new SaveData();
  foreach(int day in new[]{-7,-6,-1,0,1}) data.Plants.Add(new Plant { Task="Test",Minutes=25,Date=today.AddDays(day).AddHours(12).ToUniversalTime(),Kind=0 });
  var complete=TaskStore.Add(data,"Completed",DateTime.UtcNow); TaskStore.ToggleDone(data,complete.Id,today.AddHours(10)); TaskStore.ArchiveDone(data);
  var pending=TaskStore.Add(data,"Pending",DateTime.UtcNow);
  var week=WeekSummary.Create(data,today);
  Check(week.Start==today.AddDays(-6),"Week start"); Check(week.TotalMinutes==75 && week.TotalSessions==3,"Week bounds include outside days");
  Check(week.Minutes[0]==25 && week.Minutes[5]==25 && week.Minutes[6]==25 && week.ActiveDays==3,"Week day buckets");
  Check(week.TotalCompleted==1 && week.Completed[6]==1,"Archived completed tasks should remain in stats");
  TaskStore.ToggleDone(data,pending.Id,today.AddDays(1)); Check(WeekSummary.Create(data,today).TotalCompleted==1,"Included future completion");
  Check(WeekSummary.Create(new SaveData(),today).TotalMinutes==0,"Empty week");
  string csv=week.ToCsv(); Check(csv.StartsWith("Day,FocusMinutes,Sessions,CompletedTasks\r\n") && csv.Split('\n').Length==9,"CSV structure"); Check(csv.Contains(today.ToString("yyyy-MM-dd")+",25,1,1"),"CSV today values");
 }
 static void CheckMerging() {
  var local=new SaveData(); var remote=new SaveData(); DateTime now=DateTime.UtcNow;
  var task=TaskStore.Add(local,"Local title",now); remote.Tasks.Add(new FocusTask { Id=task.Id,Title="Remote title",Created=now });
  TaskStore.Add(remote,"Imported task",now);
  var plant=new Plant { Task="Session",Minutes=25,Date=now,Kind=2,TaskId=task.Id }; local.Plants.Add(plant); remote.Plants.Add(plant);
  var merged=Garden.MergeBackup(local,remote);
  Check(merged.Plants.Count==1 && merged.Plants[0].TaskId==task.Id,"Session merge lost task link or duplicated session");
  Check(merged.Tasks.Count==2 && merged.Tasks.First(t=>t.Id==task.Id).Title=="Local title","Merge should retain local task state");
  var again=Garden.MergeBackup(merged,remote); Check(again.Tasks.Count==2 && again.Plants.Count==1,"Import not idempotent");
  again.Tasks[0].Title="Independent result"; Check(merged.Tasks[0].Title!="Independent result" && local.Tasks[0].Title=="Local title","Merged tasks should not alias source objects");
  var huge=new SaveData(); for(int i=0;i<200;i++) TaskStore.Add(huge,"Task "+i,now);
  Reject(delegate { Garden.MergeBackup(local,huge); },"Merged queue exceeds limit"); Check(local.Tasks.Count==1,"Failed merge modified local data");
 }
 static void CheckStorage() {
  string root=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"work","storage-check-"+Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(root);
  string path=Path.Combine(root,"garden.json");
  var data=new SaveData(); TaskStore.Add(data,"Persistent task",DateTime.UtcNow); GardenStorage.Write(path,data);
  Check(File.Exists(path) && !File.Exists(path+".tmp"),"Initial atomic save");
  var loaded=GardenStorage.Load(path); Check(loaded.Notice==null && loaded.Data.Tasks.Count==1,"Read saved task queue");
  Reject(delegate { GardenStorage.Write(path,new SaveData { Task=new string('x',121) }); },"Stored an oversized task name"); Check(GardenStorage.Read(path).Tasks.Count==1,"Rejected write changed the previous save");
  TaskStore.Add(data,"Second task",DateTime.UtcNow); GardenStorage.Write(path,data);
  Check(GardenStorage.Read(path+".bak").Tasks.Count==1,"Previous save backup");
  File.WriteAllText(path,"{broken json");
  var recovered=GardenStorage.Load(path); Check(!recovered.ReadOnly && recovered.Notice!=null && recovered.Data.Tasks.Count==1,"Backup recovery failed");
  Check(Directory.GetFiles(root,"*.recovery-*").Length==1,"Corrupt file was not preserved");
  GardenStorage.Write(path,recovered.Data); Check(GardenStorage.Read(path).Tasks.Count==1,"Recovered garden not writable");
  using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)) {
   var readonlyResult=GardenStorage.Load(path); Check(readonlyResult.ReadOnly && readonlyResult.Data.Tasks.Count==1,"Locked file should fall back without overwriting");
  }
  File.WriteAllText(path,"bad"); File.WriteAllText(path+".bak","also bad");
  var empty=GardenStorage.Load(path); Check(empty.Notice!=null && empty.Data.Plants.Count==0,"Unrecoverable save handling");
  Check(Directory.GetFiles(root,"*.recovery-*").Length==3,"Original invalid files were lost");
  GardenStorage.Write(path,new SaveData()); using(var oversized=new FileStream(path,FileMode.Open,FileAccess.Write)) oversized.SetLength(5000001);
  Reject(delegate { GardenStorage.Read(path); },"Read oversized save");
 }
 static void CheckKeyboard() {
  var data=new SaveData(); DateTime now=DateTime.UtcNow; for(int i=0;i<8;i++) TaskStore.Add(data,"Queue "+i,now.AddSeconds(i));
  using(var board=new TaskBoard { Size=new System.Drawing.Size(502,250) }) {
   board.RefreshTasks(data); string selected=null,completed=null,archived=null;
   board.Selected+=delegate(string id) { selected=id; }; board.CompletionRequested+=delegate(string id) { completed=id; }; board.ArchiveRequested+=delegate(string id) { archived=id; };
   var method=typeof(TaskBoard).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
   Action<System.Windows.Forms.Keys> press=delegate(System.Windows.Forms.Keys key) { method.Invoke(board,new object[]{new System.Windows.Forms.KeyEventArgs(key)}); };
   press(System.Windows.Forms.Keys.Down); press(System.Windows.Forms.Keys.Enter); Check(selected==data.Tasks[1].Id,"Queue keyboard should select the second visible row");
   for(int i=0;i<5;i++) press(System.Windows.Forms.Keys.Down); press(System.Windows.Forms.Keys.Enter); Check(selected==data.Tasks[6].Id,"Queue keyboard scrolling");
   press(System.Windows.Forms.Keys.Space); press(System.Windows.Forms.Keys.Delete); Check(completed==data.Tasks[6].Id && archived==completed,"Queue keyboard completion and archive");
   TaskStore.ToggleDone(data,completed,now); board.RefreshTasks(data); selected=null; press(System.Windows.Forms.Keys.Enter); Check(selected==null,"Keyboard should not focus a completed task after reordering");
  }
 }
}
