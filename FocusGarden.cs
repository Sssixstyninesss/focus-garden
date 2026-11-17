using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

[assembly: System.Reflection.AssemblyTitle("Focus Garden")]
[assembly: System.Reflection.AssemblyProduct("Focus Garden")]
[assembly: System.Reflection.AssemblyVersion("1.5.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.5.1.0")]

[DataContract] public class Plant {
 [DataMember] public string Task;
 [DataMember] public int Minutes;
 [DataMember] public DateTime Date;
 [DataMember] public int Kind;
 [DataMember(EmitDefaultValue=false)] public string TaskId;
}
[DataContract] public class SaveData {
 [DataMember] public bool Dark = true;
 [DataMember] public List<Plant> Plants = new List<Plant>();
 [DataMember] public int Minutes = 25;
 [DataMember] public double Remaining = 1500;
 [DataMember] public bool Running;
 [DataMember] public bool Rest;
 [DataMember] public DateTime Deadline = DateTime.UtcNow;
 [DataMember] public string Task = "";
 [DataMember] public bool Sound = true;
 [DataMember] public bool ReducedMotion;
 [DataMember] public bool IslandPinned;
 [DataMember] public bool HasIslandPosition;
 [DataMember] public int IslandX,IslandY;
 [DataMember] public int CustomMinutes = 35;
 [DataMember] public int DailyGoal = 60;
 [DataMember] public int SelectedKind = -1;
 [DataMember] public List<FocusTask> Tasks = new List<FocusTask>();
 [DataMember] public string ActiveTaskId = "";
 [OnDeserializing] void Defaults(StreamingContext context) { Sound=true; CustomMinutes=35; DailyGoal=60; SelectedKind=-1; Tasks=new List<FocusTask>(); ActiveTaskId=""; Deadline=DateTime.UtcNow; Task=""; }
}
public class Garden : Form {
 SaveData data = new SaveData();
 string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "garden.json");
 Label clock = new Label(), hint = new Label(), stats = new Label(), title = new Label(), tagline = new Label(), taskLabel = new Label(), gardenLabel = new Label();
 TextBox task = new CueTextBox { Cue="Name your focus session" }; SessionList history = new SessionList();
 PillButton start, reset, theme, mode, export, import;
 List<PillButton> durations = new List<PillButton>();
 Panel timerPanel = new Card(), gardenPanel = new Card();
 Canvas canvas = new Canvas();
 System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
 bool loading = true; PillButton soundButton,motionButton;
 Color bg, panel, ink, muted, border, accent;
 FocusIsland island;
 NotifyIcon tray;
 bool exiting, previewMode;
 Panel windowHeader;
 PillButton minimizeButton, closeButton;
 PillButton speciesButton,goalButton;
 Canvas dailyStats=new Canvas();
 readonly List<Tuple<Rectangle,Plant>> plantHits=new List<Tuple<Rectangle,Plant>>();
 readonly System.Windows.Forms.Timer ambience=new System.Windows.Forms.Timer();
 double scenePhase;
 readonly WeekView weekView=new WeekView();
 readonly TaskBoard taskBoard=new TaskBoard();
 readonly Panel tasksPanel=new Panel();
 readonly TextBox queueInput=new CueTextBox { Cue="Add a task…" };
 readonly Label queueCount=new Label();
 List<Control> greenhouseControls;
 PillButton gardenTab,weekTab,tasksTab;
 string currentView="Garden";
 bool storageReadOnly;
 Label historyHeading;
 PillButton resetHistory;
 PillButton archiveViewButton;
 DateTime displayedDay=DateTime.Today,cachedDay=DateTime.MinValue;
 int revision,todayRevision=-1,cachedTodayMinutes;
 internal bool IslandModeRequested;
 readonly System.Windows.Forms.Timer saveDelay=new System.Windows.Forms.Timer();
 [STAThread] public static void Main(string[] args) {
  if (args.Contains("--self-test")) { SelfTest(); return; }
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  if (args.Contains("--render-preview")) { RenderPreview(args.Last()); return; }
  if (args.Contains("--render-motion")) { using(var sample=new Garden(true)) { sample.SetIslandTask("Design the next update"); sample.data.Plants.Add(new Plant { Task="Read a chapter",Minutes=25,Date=DateTime.UtcNow,Kind=2 }); sample.data.Remaining=1122; using(var capsule=new FocusIsland(sample)) capsule.RenderMotionFrames(args.Last()); } return; }
  using (var mutex = new System.Threading.Mutex(true,"Local\\FocusGardenDesktop",out single)) { if (!single) { MessageBox.Show("Focus Garden is already running."); return; } Application.Run(new Garden()); }
 }
 static bool single;
 static void SelfTest() {
  var d = new SaveData(); d.Plants.Add(new Plant { Task = "Test", Minutes = 25, Date = DateTime.UtcNow, Kind = 2 });
  d.Sound=false; d.ReducedMotion=true; d.IslandPinned=true; d.HasIslandPosition=true; d.IslandX=500; d.IslandY=30; d.CustomMinutes=37;
  using(var ms = new MemoryStream()) { Serializer().WriteObject(ms,d); ms.Position=0; var copy=(SaveData)Serializer().ReadObject(ms); Validate(copy); if(copy.Plants[0].Task!="Test" || !copy.Dark || copy.Sound || !copy.ReducedMotion || !copy.IslandPinned || copy.IslandX!=500 || copy.CustomMinutes!=37) throw new Exception("Serialization failed"); }
  if (Format(65)!="01:05" || Seconds(DateTime.UtcNow.AddSeconds(-2))!=0) throw new Exception("Timer failed");
  bool rejected=false; try { Validate(new SaveData { Minutes=-1 }); } catch { rejected=true; } if(!rejected) throw new Exception("Invalid duration accepted");
  var presence=new IslandPresence(); presence.Wake(0);
  if(presence.Update(1,false,false,false) || presence.Sleeping) throw new Exception("Hidden too early");
  if(!presence.Update(2,false,false,false) || !presence.Sleeping) throw new Exception("Auto-hide failed");
  if(presence.Update(2.1,false,true,false)) throw new Exception("Hover delay missing");
  if(!presence.Update(2.3,false,true,false) || presence.Sleeping) throw new Exception("Hover reveal failed");
  presence.Pinned=true; presence.Update(20,false,false,false); if(presence.Sleeping) throw new Exception("Pin failed");
  presence.Pinned=false; presence.Enabled=false; presence.Update(50,false,true,false); if(presence.Sleeping) throw new Exception("Suspended island changed state");
  using(var legacy=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Dark\":true,\"Minutes\":25,\"Remaining\":1500,\"Plants\":[],\"Task\":\"\"}"))) { var migrated=(SaveData)Serializer().ReadObject(legacy); Validate(migrated); if(!migrated.Sound || migrated.CustomMinutes!=35) throw new Exception("Legacy settings migration failed"); }
  int parsed; foreach(string invalid in new[]{"0","121","-1","1.5","abc",""}) if(DurationPicker.TryParseMinutes(invalid,out parsed)) throw new Exception("Accepted an invalid duration");
  if(!DurationPicker.TryParseMinutes("37",out parsed) || parsed!=37) throw new Exception("Rejected a valid duration");
  DateTime today=DateTime.Today; var days=new List<Plant>(); foreach(int ago in new[]{0,0,1,2,4}) days.Add(new Plant { Date=today.AddDays(-ago).ToUniversalTime() });
  if(CountStreak(days,today)!=3) throw new Exception("Streak gap or duplicate handling failed");
  if(CountStreak(days.Where(p=>p.Date.ToLocalTime().Date!=today),today)!=2) throw new Exception("Yesterday streak should remain active");
  if(CountStreak(new Plant[0],today)!=0) throw new Exception("Empty streak failed");
  Console.WriteLine("PASS: persistence, migration, validation, timer, streaks, durations, island presence");
 }
 static DataContractJsonSerializer Serializer() { return new DataContractJsonSerializer(typeof(SaveData)); }
 internal static void Validate(SaveData d) {
  if(d==null || d.Plants==null || d.Plants.Count>10000 || d.Minutes<1 || d.Minutes>120 || d.CustomMinutes<1 || d.CustomMinutes>120 || d.DailyGoal<1 || d.DailyGoal>240 || d.SelectedKind< -1 || d.SelectedKind>3 || d.Task==null || d.Task.Length>120 || d.ActiveTaskId==null || d.ActiveTaskId.Length>64 || d.Deadline==DateTime.MinValue || d.Deadline==DateTime.MaxValue || double.IsNaN(d.Remaining) || double.IsInfinity(d.Remaining) || d.Remaining<0 || d.Remaining>7200) throw new Exception("Invalid garden file.");
  foreach(var p in d.Plants) if(p==null || p.Task==null || p.Task.Length>120 || (p.TaskId!=null && p.TaskId.Length>64) || p.Minutes<1 || p.Minutes>120 || p.Kind<0 || p.Kind>3 || p.Date==DateTime.MinValue || p.Date==DateTime.MaxValue) throw new Exception("Invalid plant data.");
  if(d.Tasks==null || d.Tasks.Count>5000 || d.Tasks.Any(t=>t==null || String.IsNullOrWhiteSpace(t.Id) || t.Id.Length>64 || String.IsNullOrWhiteSpace(t.Title) || t.Title.Length>120 || t.Done!=t.CompletedAt.HasValue || t.Created==DateTime.MinValue || t.Created==DateTime.MaxValue || t.CompletedAt==DateTime.MinValue || t.CompletedAt==DateTime.MaxValue) || d.Tasks.Select(t=>t.Id).Distinct().Count()!=d.Tasks.Count || d.Tasks.Count(t=>!t.Archived)>200) throw new Exception("Invalid task data.");
 }
 static string Format(double seconds) { int s=(int)Math.Ceiling(seconds); return (s/60).ToString("00")+":"+(s%60).ToString("00"); }
 static double Seconds(DateTime deadline) { return Math.Max(0,(deadline-DateTime.UtcNow).TotalSeconds); }
 public static SaveData MergeBackup(SaveData local,SaveData incoming) {
  Validate(local); Validate(incoming); SaveData result;
  using(var stream=new MemoryStream()) { Serializer().WriteObject(stream,local); stream.Position=0; result=(SaveData)Serializer().ReadObject(stream); }
  result.Plants=local.Plants.Concat(incoming.Plants).GroupBy(p=>p.Date.ToString("o")+"|"+p.Task+"|"+p.Minutes).Select(g=>g.First()).OrderBy(p=>p.Date).Select(p=>new Plant { Task=p.Task,TaskId=p.TaskId,Minutes=p.Minutes,Date=p.Date,Kind=p.Kind }).ToList();
  result.Tasks=local.Tasks.Concat(incoming.Tasks).GroupBy(t=>t.Id).Select(g=>g.First()).OrderBy(t=>t.Created).Select(t=>new FocusTask { Id=t.Id,Title=t.Title,Created=t.Created,Done=t.Done,Archived=t.Archived,CompletedAt=t.CompletedAt }).ToList();
  Validate(result); return result;
 }
 public Garden() : this(false) { }
 public Garden(bool preview) {
  previewMode=preview;
  Text="Focus Garden"; Icon=LoadIcon(); FormBorderStyle=FormBorderStyle.None; ClientSize=new Size(1060,792); MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); DoubleBuffered=true;
  string loadNotice=null;
  if(!previewMode) { var loaded=GardenStorage.Load(savePath); data=loaded.Data; storageReadOnly=loaded.ReadOnly; loadNotice=loaded.Notice; }
  title=LabelAt(this,"Focus Garden",82,22,400,46,24,FontStyle.Bold);
  var mark=new PictureBox { Image=LoadMark(),SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(34,28),Size=new Size(36,36) }; Controls.Add(mark);
  tagline=LabelAt(this,"DESKTOP FOCUS TIMER",34,78,550,28,10,FontStyle.Regular);
  theme=ButtonAt(this,"Light theme",820,28,200,38,delegate { data.Dark=!data.Dark; ApplyTheme(); Save(); });
  soundButton=ButtonAt(this,data.Sound?"Sound: on":"Sound: off",820,76,200,32,delegate { data.Sound=!data.Sound; soundButton.Text=data.Sound?"Sound: on":"Sound: off"; Save(); });
  motionButton=ButtonAt(this,data.ReducedMotion?"Motion: reduced":"Motion: full",610,76,190,32,delegate { data.ReducedMotion=!data.ReducedMotion; motionButton.Text=data.ReducedMotion?"Motion: reduced":"Motion: full"; ApplyMotion(this); if(island!=null) island.ApplyMotion(); Save(); });
  ButtonAt(this,"Focus Island",610,28,190,38,ShowIsland);
  timerPanel.SetBounds(30,130,430,430); Controls.Add(timerPanel);
  gardenPanel.SetBounds(480,130,550,430); Controls.Add(gardenPanel);
  LabelAt(timerPanel,"FOCUS",24,20,240,24,10,FontStyle.Regular);
  ButtonAt(timerPanel,"Custom time",286,16,120,28,delegate { ChooseDuration(this); });
  mode=ButtonAt(timerPanel,"Focus · switch to break",24,58,382,36,delegate { if(data.Running) { hint.Text="Pause the timer before switching modes."; return; } data.Rest=!data.Rest; SetDuration(data.Rest?5:25); });
  clock=LabelAt(timerPanel,"25:00",20,102,390,98,64,FontStyle.Regular); clock.TextAlign=ContentAlignment.MiddleCenter;
  int x=24; foreach(int value in new[]{15,25,50,1}) { int minutes=value; var b=ButtonAt(timerPanel,value==1?"1m demo":value+" min",x,214,88,34,delegate { if(!data.Running) SetDuration(minutes); }); durations.Add(b); x+=98; }
  taskLabel=LabelAt(timerPanel,"What are you working on?",24,266,380,24,10,FontStyle.Regular);
  task.SetBounds(24,295,382,30); task.MaxLength=120; task.Text=data.Task??""; timerPanel.Controls.Add(task);
  task.BorderStyle=BorderStyle.FixedSingle;
  task.TextChanged+=delegate { data.Task=task.Text; if(!loading) { TaskStore.RenameActive(data,data.Task); ScheduleSave(); if(taskBoard.Visible) taskBoard.RefreshTasks(data); } };
  start=ButtonAt(timerPanel,"Start focus",24,350,280,48,Toggle);
  reset=ButtonAt(timerPanel,"Reset",314,350,92,48,delegate { data.Running=false; data.Remaining=data.Minutes*60; Save(); Render(); });
  gardenLabel=LabelAt(gardenPanel,"YOUR GARDEN",24,20,490,28,10,FontStyle.Regular);
  LabelAt(gardenPanel,"The greenhouse",24,64,500,42,22,FontStyle.Bold);
  speciesButton=ButtonAt(gardenPanel,"Mixed seeds",364,16,162,28,delegate { data.SelectedKind=data.SelectedKind==3?-1:data.SelectedKind+1; Save(); Render(); });
  canvas.SetBounds(0,120,550,230); gardenPanel.Controls.Add(canvas); canvas.Paint+=DrawGarden;
  canvas.MouseMove+=delegate(object sender,MouseEventArgs e) { canvas.Cursor=plantHits.Any(hit=>hit.Item1.Contains(e.Location))?Cursors.Hand:Cursors.Default; };
  canvas.MouseClick+=delegate(object sender,MouseEventArgs e) { var hit=plantHits.LastOrDefault(item=>item.Item1.Contains(e.Location)); if(hit!=null) hint.Text=(String.IsNullOrWhiteSpace(hit.Item2.Task)?"Untitled session":hit.Item2.Task)+" · "+hit.Item2.Minutes+" min · "+hit.Item2.Date.ToLocalTime().ToString("MMM dd, HH:mm",System.Globalization.CultureInfo.InvariantCulture); };
  stats=LabelAt(gardenPanel,"",20,365,510,42,15,FontStyle.Regular); stats.TextAlign=ContentAlignment.MiddleCenter;
  stats.Visible=false; dailyStats.SetBounds(24,360,502,65); gardenPanel.Controls.Add(dailyStats); dailyStats.Paint+=DrawDailyStats;
  greenhouseControls=gardenPanel.Controls.Cast<Control>().ToList();
  weekView.SetBounds(24,64,502,364); gardenPanel.Controls.Add(weekView);
  weekView.ExportRequested+=ExportWeek;
  weekView.DaySelected+=delegate(DateTime day) { historyHeading.Text=day.ToString("MMM dd",System.Globalization.CultureInfo.InvariantCulture)+" sessions"; history.EmptyMessage="No focus sessions on this day."; history.SetEntries(data.Plants.Where(p=>p.Date.ToLocalTime().Date==day.Date).OrderByDescending(p=>p.Date).Take(30)); resetHistory.Visible=true; hint.Text="Showing focus sessions for "+day.ToString("MMMM dd",System.Globalization.CultureInfo.InvariantCulture)+"."; };
  tasksPanel.SetBounds(24,64,502,364); gardenPanel.Controls.Add(tasksPanel);
  LabelAt(tasksPanel,"Task queue",0,0,232,40,22,FontStyle.Bold);
  archiveViewButton=ButtonAt(tasksPanel,"Archived",248,3,94,29,delegate { taskBoard.ShowArchived=!taskBoard.ShowArchived; RefreshPlanning(); });
  queueCount.SetBounds(3,40,490,22); queueCount.Font=new Font("Segoe UI",8); tasksPanel.Controls.Add(queueCount);
  ButtonAt(tasksPanel,"Archive done",350,3,152,29,delegate { TaskStore.ArchiveDone(data); Save(); RefreshPlanning(); });
  queueInput.SetBounds(3,72,375,24); queueInput.BorderStyle=BorderStyle.None; queueInput.MaxLength=120; queueInput.AccessibleName="New task name"; tasksPanel.Controls.Add(queueInput);
  ButtonAt(tasksPanel,"Add task",390,65,112,34,AddQueueTask);
  queueInput.KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Enter) { AddQueueTask(); e.SuppressKeyPress=true; } };
  taskBoard.SetBounds(0,112,502,250); tasksPanel.Controls.Add(taskBoard);
  taskBoard.Selected+=delegate(string id) { SelectQueuedTask(id); };
  taskBoard.CompletionRequested+=delegate(string id) { CompleteQueuedTask(id); };
  taskBoard.ArchiveRequested+=delegate(string id) { bool changed=taskBoard.ShowArchived?TaskStore.Restore(data,id):TaskStore.Archive(data,id); if(!changed) { hint.Text=taskBoard.ShowArchived?"The visible task queue is full. Archive another task before restoring this one.":"Pause the active session before archiving its task."; return; } task.Text=data.Task; Save(); RefreshPlanning(); };
  gardenTab=ButtonAt(gardenPanel,"Garden",24,16,100,29,delegate { SwitchView("Garden"); });
  weekTab=ButtonAt(gardenPanel,"Week",132,16,100,29,delegate { SwitchView("Week"); });
  tasksTab=ButtonAt(gardenPanel,"Tasks",240,16,100,29,delegate { SwitchView("Tasks"); });
  hint=LabelAt(this,"Your garden is saved on this device.",32,570,990,28,10,FontStyle.Regular); hint.AccessibleRole=AccessibleRole.StaticText;
  historyHeading=LabelAt(this,"Recent sessions",32,610,400,28,16,FontStyle.Bold);
  resetHistory=ButtonAt(this,"All sessions",600,610,130,26,ResetHistory); resetHistory.Visible=false;
  history.SetBounds(32,648,698,82); history.SessionSelected+=delegate(Plant selected) { hint.Text=(String.IsNullOrWhiteSpace(selected.Task)?"Untitled session":selected.Task)+" · "+selected.Minutes+" min · "+selected.Date.ToLocalTime().ToString("MMM dd, HH:mm",System.Globalization.CultureInfo.InvariantCulture); }; Controls.Add(history);
  export=ButtonAt(this,"Export garden",760,640,270,38,Export);
  import=ButtonAt(this,"Import garden",760,688,270,38,Import);
  goalButton=ButtonAt(this,"Daily goal · 60m",760,600,270,30,delegate { data.DailyGoal=data.DailyGoal>=120?30:data.DailyGoal+30; Save(); Render(); });
  foreach(Control control in Controls) control.Top+=32;
  windowHeader=new Panel { Location=Point.Empty,Size=new Size(1060,32) }; Controls.Add(windowHeader);
  var windowName=LabelAt(windowHeader,"Focus Garden  ·  1.5.1",26,4,300,24,9,FontStyle.Regular);
  bool moving=false; Point mouseOrigin=Point.Empty,windowOrigin=Point.Empty;
  MouseEventHandler down=delegate(object sender,MouseEventArgs e) { if(e.Button!=MouseButtons.Left) return; moving=true; mouseOrigin=Cursor.Position; windowOrigin=Location; ((Control)sender).Capture=true; };
  MouseEventHandler move=delegate { if((Control.MouseButtons&MouseButtons.Left)==0) moving=false; if(moving) Location=new Point(windowOrigin.X+Cursor.Position.X-mouseOrigin.X,windowOrigin.Y+Cursor.Position.Y-mouseOrigin.Y); };
  MouseEventHandler up=delegate(object sender,MouseEventArgs e) { moving=false; ((Control)sender).Capture=false; };
  windowHeader.MouseDown+=down; windowHeader.MouseMove+=move; windowHeader.MouseUp+=up;
  windowName.MouseDown+=down; windowName.MouseMove+=move; windowName.MouseUp+=up;
  windowHeader.MouseCaptureChanged+=delegate { if((Control.MouseButtons&MouseButtons.Left)==0) moving=false; };
  windowName.MouseCaptureChanged+=delegate { if((Control.MouseButtons&MouseButtons.Left)==0) moving=false; };
  minimizeButton=ButtonAt(windowHeader,"−",958,4,40,24,delegate { WindowState=FormWindowState.Minimized; }); minimizeButton.AccessibleName="Minimize window";
  closeButton=ButtonAt(windowHeader,"×",1006,4,40,24,ShowIsland); closeButton.AccessibleName="Return to island";
  using(var shape=Shapes.Round(ClientRectangle,22)) Region=new Region(shape);
  Paint+=delegate(object sender,PaintEventArgs e) { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var outline=new Pen(border)) using(var shape=Shapes.Round(new Rectangle(0,0,Width-1,Height-1),22)) e.Graphics.DrawPath(outline,shape); };
  Resize+=delegate { int extra=ClientSize.Width-1060; theme.Left=820+extra; soundButton.Left=820+extra; };
  FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(e.CloseReason==CloseReason.UserClosing && !exiting) { e.Cancel=true; Save(); ShowIsland(); return; } exiting=true; if(data.Running) data.Remaining=Seconds(data.Deadline); Save(); tick.Stop(); ambience.Stop(); if(island!=null) { island.Close(); island.Dispose(); } if(tray!=null) { tray.Visible=false; tray.Dispose(); } };
  ambience.Interval=80; ambience.Tick+=delegate { if(!Visible || !canvas.Visible || data.ReducedMotion) { ambience.Stop(); return; } scenePhase+=.07; canvas.Invalidate(); };
  VisibleChanged+=delegate { if(Visible && canvas.Visible && !data.ReducedMotion) ambience.Start(); else ambience.Stop(); };
  loading=false; ApplyTheme(); Render(); tick.Interval=250; tick.Tick+=delegate { UpdateTimer(); }; tick.Start(); UpdateTimer();
  saveDelay.Interval=450; saveDelay.Tick+=delegate { Save(); };
  if(loadNotice!=null) { hint.Text=loadNotice; MessageBox.Show(loadNotice,"Focus Garden recovery",MessageBoxButtons.OK,MessageBoxIcon.Information); }
  if(!previewMode) {
   var menu=new ContextMenuStrip(); var trayToggle=menu.Items.Add("Start / pause",null,delegate { ToggleFromIsland(); }); menu.Opening+=delegate { trayToggle.Text=data.Running?"Pause session":"Start / resume session"; }; menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Show island",null,delegate { ShowIsland(); }); menu.Items.Add("Open garden",null,delegate { ShowGarden(); }); menu.Items.Add("Quit",null,delegate { ExitFromIsland(); });
   tray=new NotifyIcon { Icon=Icon,Text="Focus Garden",ContextMenuStrip=menu,Visible=true };
   tray.DoubleClick+=delegate { ShowGarden(); };
   Shown+=delegate { ShowIsland(); };
  }
 }
 public SaveData State { get { return data; } }
 public static string PlantName(int kind) { return new[]{"Sunflower","Pine","Blossom","Mushroom"}[Math.Max(0,Math.Min(3,kind))]; }
 public int TodayMinutes { get { if(cachedDay!=DateTime.Today || todayRevision!=revision) { cachedDay=DateTime.Today; todayRevision=revision; cachedTodayMinutes=data.Plants.Where(p=>p.Date.ToLocalTime().Date==cachedDay).Sum(p=>p.Minutes); } return cachedTodayMinutes; } }
 public static int CountStreak(IEnumerable<Plant> plants,DateTime today) { var dates=new HashSet<DateTime>(plants.Select(p=>p.Date.ToLocalTime().Date)); DateTime day=dates.Contains(today.Date)?today.Date:today.Date.AddDays(-1); int count=0; while(dates.Contains(day)) { count++; day=day.AddDays(-1); } return count; }
 public string TimeText { get { return Format(data.Running?Seconds(data.Deadline):data.Remaining); } }
 public double Progress { get { return Math.Max(0,Math.Min(1,1-(data.Running?Seconds(data.Deadline):data.Remaining)/(data.Minutes*60.0))); } }
 public void ToggleFromIsland() { UpdateTimer(); Toggle(); }
 public void IslandDuration(int minutes) { if(!data.Running) SetDuration(minutes); }
 public void IslandBreak() { if(data.Running) return; data.Rest=!data.Rest; SetDuration(data.Rest?5:25); }
 public void SetIslandTask(string value) { task.Text=(value??"").Substring(0,Math.Min(120,(value??"").Length)); }
 void AddQueueTask() { try { TaskStore.Add(data,queueInput.Text,DateTime.UtcNow); queueInput.Clear(); taskBoard.ShowArchived=false; Save(); RefreshPlanning(); hint.Text="Task added to your queue."; } catch(Exception error) { hint.Text=error.Message; } }
 public bool SelectQueuedTask(string id) { if(!TaskStore.Select(data,id)) { hint.Text="Pause the timer before selecting a task."; return false; } task.Text=data.Task; Save(); RefreshPlanning(); return true; }
 public bool CompleteQueuedTask(string id) { if(!TaskStore.ToggleDone(data,id,DateTime.UtcNow)) { hint.Text="Pause the timer before completing its active task."; return false; } task.Text=data.Task; Save(); RefreshPlanning(); return true; }
 public void ShowTasks() { ShowGarden(); SwitchView("Tasks"); }
 void RefreshPlanning() { weekView.RefreshData(data); taskBoard.RefreshTasks(data); queueCount.Text=taskBoard.ShowArchived?data.Tasks.Count(t=>t.Archived)+" archived  ·  Use ↶ to restore a task":data.Tasks.Count(t=>!t.Done && !t.Archived)+" pending  ·  "+data.Tasks.Count(t=>t.Done && !t.Archived)+" completed"; archiveViewButton.FillColor=taskBoard.ShowArchived?accent:border; archiveViewButton.ForeColor=taskBoard.ShowArchived?(data.Dark?bg:Color.White):ink; }
 void ResetHistory() { historyHeading.Text="Recent sessions"; history.EmptyMessage="Completed sessions will appear here."; history.SetEntries(data.Plants.OrderByDescending(p=>p.Date).Take(30)); resetHistory.Visible=false; }
 void ExportWeek() { var summary=WeekSummary.Create(data,DateTime.Today); using(var dialog=new SaveFileDialog { Filter="CSV (*.csv)|*.csv",FileName="focus-week-"+summary.Start.ToString("yyyy-MM-dd")+".csv" }) if(dialog.ShowDialog(this)==DialogResult.OK) try { File.WriteAllText(dialog.FileName,summary.ToCsv(),new System.Text.UTF8Encoding(true)); hint.Text="Weekly statistics exported."; } catch(Exception error) { hint.Text="Export failed: "+error.Message; } }
 void SwitchView(string name) {
  currentView=name; foreach(var c in greenhouseControls) c.Visible=name=="Garden" && c!=gardenLabel && c!=stats;
  weekView.Visible=name=="Week"; tasksPanel.Visible=name=="Tasks";
  gardenTab.FillColor=name=="Garden"?accent:border; gardenTab.ForeColor=name=="Garden"?(data.Dark?bg:Color.White):ink;
  weekTab.FillColor=name=="Week"?accent:border; weekTab.ForeColor=name=="Week"?(data.Dark?bg:Color.White):ink;
  tasksTab.FillColor=name=="Tasks"?accent:border; tasksTab.ForeColor=name=="Tasks"?(data.Dark?bg:Color.White):ink;
  RefreshPlanning();
 }
 public void RememberIsland(Point anchor,bool pinned) { data.HasIslandPosition=true; data.IslandX=anchor.X; data.IslandY=anchor.Y; data.IslandPinned=pinned; Save(); }
 public void SetCustomDuration(int minutes) { if(minutes<1 || minutes>120) throw new ArgumentOutOfRangeException("minutes"); if(data.Running) return; data.CustomMinutes=minutes; SetDuration(minutes); }
 public void ChooseDuration(IWin32Window owner) { if(data.Running) { hint.Text="Pause the session before changing its duration."; return; } using(var picker=new DurationPicker(data.CustomMinutes,data.ReducedMotion)) if(picker.ShowDialog(owner)==DialogResult.OK) SetCustomDuration(picker.Minutes); }
 void ApplyMotion(Control parent) { foreach(Control c in parent.Controls) { var button=c as PillButton; if(button!=null) button.ReducedMotion=data.ReducedMotion; ApplyMotion(c); } }
 public void ShowGarden() { if(island!=null) island.Suspend(); Show(); WindowState=FormWindowState.Normal; Activate(); }
 public void ShowIsland() { if(exiting) return; IslandModeRequested=true; Save(); if(previewMode) { Hide(); return; } if(island==null || island.IsDisposed) island=new FocusIsland(this); island.Reveal(); Hide(); }
 public static Icon LoadIcon() { using(var stream=typeof(Garden).Assembly.GetManifestResourceStream("FocusGarden.Icon")) { if(stream==null) return (Icon)SystemIcons.Application.Clone(); using(var icon=new Icon(stream,new Size(32,32))) return (Icon)icon.Clone(); } }
 public static Image LoadMark() { using(var stream=typeof(Garden).Assembly.GetManifestResourceStream("FocusGarden.Mark")) { if(stream==null) return SystemIcons.Application.ToBitmap(); using(var image=Image.FromStream(stream)) return new Bitmap(image); } }
 public void ExitFromIsland() { exiting=true; Close(); }
 internal static void RenderPreview(string folder) {
  Directory.CreateDirectory(folder);
  using(var garden=new Garden(true)) {
   garden.data.Task="Design the next update"; garden.task.Text=garden.data.Task;
   garden.data.Plants.Add(new Plant { Task="Read a chapter",Minutes=25,Date=DateTime.UtcNow,Kind=2 });
   garden.data.Remaining=18*60+42; garden.Render();
   using(var capsule=new FocusIsland(garden)) {
    capsule.CheckLifecycle();
    garden.data.ReducedMotion=true; capsule.ApplyMotion(); capsule.CheckLifecycle(); garden.data.ReducedMotion=false; capsule.ApplyMotion();
    capsule.CreateControl(); capsule.RenderTo(Path.Combine(folder,"island-compact-1.5.1-en.png"),false);
    capsule.RenderTo(Path.Combine(folder,"island-expanded-1.5.1-en.png"),true);
    garden.SetCustomDuration(37); if(garden.data.Remaining!=2220 || garden.data.CustomMinutes!=37) throw new Exception("Custom duration failed");
    garden.SetIslandTask("Write a project note"); if(garden.data.Task!="Write a project note") throw new Exception("Island task did not sync");
    garden.data.Remaining=1122;
    garden.ToggleFromIsland(); if(!garden.data.Running) throw new Exception("Island start failed");
    garden.SetCustomDuration(45); if(garden.data.Minutes!=37) throw new Exception("Changed duration during a running session");
    garden.ToggleFromIsland(); if(garden.data.Running || garden.data.Remaining>1123) throw new Exception("Island pause failed");
    garden.IslandBreak(); if(!garden.data.Rest || garden.data.Minutes!=5) throw new Exception("Island break failed");
    garden.IslandBreak(); garden.data.SelectedKind=2; garden.data.Deadline=DateTime.UtcNow.AddSeconds(-2); garden.data.Running=true; garden.UpdateTimer();
    if(garden.data.Running || garden.data.Plants.Count!=2) throw new Exception("Completed session did not grow a plant");
    if(garden.data.Plants.Last().Kind!=2) throw new Exception("Selected seed was not used");
    garden.UpdateTimer(); if(garden.data.Plants.Count!=2) throw new Exception("Duplicate plant after completion");
    capsule.Celebrate(); capsule.RenderTo(Path.Combine(folder,"island-celebration-1.5.1-en.png"),true);
   }
   Point savedAnchor=new Point(Screen.PrimaryScreen.WorkingArea.Left+Screen.PrimaryScreen.WorkingArea.Width/2,Screen.PrimaryScreen.WorkingArea.Top+16);
   garden.RememberIsland(savedAnchor,true);
   using(var restored=new FocusIsland(garden)) if(restored.Left+restored.Width/2!=savedAnchor.X || restored.Top!=savedAnchor.Y) throw new Exception("Island position restore failed");
   garden.data.IslandPinned=false;
   using(var picker=new DurationPicker(37)) { picker.StartPosition=FormStartPosition.Manual; picker.Location=new Point(-30000,-30000); picker.Show(); using(var bmp=new Bitmap(picker.Width,picker.Height)) { picker.DrawToBitmap(bmp,new Rectangle(Point.Empty,picker.Size)); bmp.Save(Path.Combine(folder,"custom-duration-1.5.1-en.png")); } picker.Hide(); }
   for(int i=0;i<10;i++) garden.data.Plants.Add(new Plant { Task=new[]{"Sketch the layout","Read a chapter","Review the code","Write a project note"}[i%4],Minutes=25,Date=DateTime.UtcNow.AddDays(-1-i/3),Kind=i%4 });
   garden.IslandDuration(25);
   var next=TaskStore.Add(garden.data,"Design the next update",DateTime.UtcNow.AddHours(-2));
   TaskStore.Add(garden.data,"Review open issues",DateTime.UtcNow.AddHours(-1)); TaskStore.Add(garden.data,"Write the release notes",DateTime.UtcNow);
   for(int i=0;i<3;i++) { var done=TaskStore.Add(garden.data,new[]{"Sketch the interface","Test the timer","Refine the icon"}[i],DateTime.UtcNow.AddDays(-4)); TaskStore.ToggleDone(garden.data,done.Id,DateTime.UtcNow.AddDays(-i)); }
   garden.SelectQueuedTask(next.Id); garden.Render();
   garden.StartPosition=FormStartPosition.Manual; garden.Location=new Point(-30000,-30000); garden.ShowInTaskbar=false; garden.Show();
   using(var bmp=new Bitmap(garden.Width,garden.Height)) { garden.DrawToBitmap(bmp,new Rectangle(0,0,garden.Width,garden.Height)); bmp.Save(Path.Combine(folder,"dark-theme-1.5.1-en.png")); }
   garden.SwitchView("Week"); using(var bmp=new Bitmap(garden.Width,garden.Height)) { garden.DrawToBitmap(bmp,new Rectangle(Point.Empty,garden.Size)); bmp.Save(Path.Combine(folder,"week-1.5.1-en.png")); }
   garden.SwitchView("Tasks"); using(var bmp=new Bitmap(garden.Width,garden.Height)) { garden.DrawToBitmap(bmp,new Rectangle(Point.Empty,garden.Size)); bmp.Save(Path.Combine(folder,"tasks-1.5.1-en.png")); }
   garden.SwitchView("Garden");
   garden.data.Dark=false; garden.ApplyTheme();
   using(var bmp=new Bitmap(garden.Width,garden.Height)) { garden.DrawToBitmap(bmp,new Rectangle(0,0,garden.Width,garden.Height)); bmp.Save(Path.Combine(folder,"light-theme-1.5.1-en.png")); }
   garden.Hide();
  }
 }
 Label LabelAt(Control parent,string text,int x,int y,int w,int h,int size,FontStyle style) { var l=new Label { Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",size,style),BackColor=Color.Transparent }; parent.Controls.Add(l); return l; }
 PillButton ButtonAt(Control parent,string text,int x,int y,int w,int h,Action action) { var b=new PillButton { UseControlColors=true,Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",10),TabStop=true }; b.Click+=delegate { action(); }; parent.Controls.Add(b); return b; }
 void ApplyTheme() {
  bg=ColorTranslator.FromHtml(data.Dark?"#101418":"#EFF2F3"); panel=ColorTranslator.FromHtml(data.Dark?"#192126":"#FFFFFF"); ink=ColorTranslator.FromHtml(data.Dark?"#EBF0F1":"#1C2A2E"); muted=ColorTranslator.FromHtml(data.Dark?"#95A8AF":"#65777D"); border=ColorTranslator.FromHtml(data.Dark?"#29363C":"#E3EBEB"); accent=ColorTranslator.FromHtml(data.Dark?"#A7E3BC":"#275C48");
  BackColor=bg; PaintControls(this); timerPanel.BackColor=panel; gardenPanel.BackColor=panel; canvas.BackColor=panel;
  windowHeader.BackColor=bg; windowHeader.Controls[0].ForeColor=muted; minimizeButton.FillColor=bg; closeButton.FillColor=bg;
  history.BackColor=panel; history.ForeColor=ink; history.Subtle=muted; history.Highlight=accent;
  tasksPanel.BackColor=panel; queueInput.BackColor=border; queueCount.ForeColor=muted;
  taskBoard.BackColor=panel; taskBoard.ForeColor=ink; taskBoard.Muted=muted; taskBoard.Accent=accent;
  weekView.BackColor=panel; weekView.ForeColor=ink; weekView.Muted=muted; weekView.Accent=accent;
  ApplyMotion(this);
  if(Visible && canvas.Visible && !data.ReducedMotion) ambience.Start(); else ambience.Stop();
  theme.Text=data.Dark?"Light theme":"Dark theme"; start.FillColor=accent; start.ForeColor=data.Dark?bg:Color.White; hint.ForeColor=muted; tagline.ForeColor=muted; stats.ForeColor=muted; canvas.Invalidate();
  SwitchView(currentView);
 }
 void PaintControls(Control root) { foreach(Control c in root.Controls) { c.ForeColor=ink; if(c is PillButton) ((PillButton)c).FillColor=border; else if(c is TextBox || c is ListBox) c.BackColor=panel; PaintControls(c); } }
 void SetDuration(int minutes) { data.Minutes=minutes; data.Remaining=minutes*60; data.Running=false; Save(); Render(); }
 void Toggle() { if(data.Running) { data.Remaining=Seconds(data.Deadline); data.Running=false; hint.Text="Session paused."; } else { data.Deadline=DateTime.UtcNow.AddSeconds(data.Remaining); data.Running=true; hint.Text="Session in progress."; } tick.Interval=data.Running?250:1000; Save(); Render(); }
 void UpdateTimer() {
  if(displayedDay!=DateTime.Today) { displayedDay=DateTime.Today; Render(); }
  if(data.Running) {
   data.Remaining=Seconds(data.Deadline);
   if(data.Remaining<=0) { data.Running=false; if(!data.Rest) { data.Plants.Add(new Plant { Task=task.Text.Trim(),TaskId=data.ActiveTaskId,Minutes=data.Minutes,Date=DateTime.UtcNow,Kind=data.SelectedKind<0?new Random().Next(4):data.SelectedKind }); hint.Text="Session complete. A new plant has been added."; } else hint.Text="Break complete."; data.Remaining=data.Minutes*60; Save(); if(data.Sound && !previewMode) System.Media.SystemSounds.Asterisk.Play(); if(island!=null) island.Celebrate(!data.Rest); Render(); }
  }
  clock.Text=Format(data.Remaining); Text=data.Running?clock.Text+" · Focus Garden":"Focus Garden";
  tick.Interval=data.Running?250:1000;
 }
 void Render() {
  revision++;
  tagline.Text="FOCUS TIMER  /  "+data.Plants.Sum(p=>p.Minutes)+" TOTAL MINUTES";
  clock.Text=Format(data.Remaining); start.Text=data.Running?"Pause session":data.Remaining<data.Minutes*60?"Resume session":data.Rest?"Start break":"Start focus";
  mode.Text=data.Rest?"Break · switch to focus":"Focus · switch to break";
  speciesButton.Text=data.SelectedKind<0?"Seeds · Mixed":"Seeds · "+PlantName(data.SelectedKind); speciesButton.Enabled=!data.Running;
  goalButton.Text="Daily goal · "+data.DailyGoal+"m"; dailyStats.Invalidate();
  foreach(var b in durations) { b.Enabled=!data.Running; b.ForeColor=ink; b.FillColor=border; }
  stats.Text="Plants: "+data.Plants.Count+"     /     Focus: "+data.Plants.Sum(p=>p.Minutes)+" min     /     Today: "+data.Plants.Count(p=>p.Date.ToLocalTime().Date==DateTime.Today);
  ResetHistory(); canvas.Invalidate();
  RefreshPlanning();
 }
 void ScheduleSave() { if(previewMode) return; saveDelay.Stop(); saveDelay.Start(); }
 void Save() { if(previewMode) return; saveDelay.Stop(); if(storageReadOnly) { hint.Text="Automatic saving is disabled. Export your garden to a writable folder."; return; } try { GardenStorage.Write(savePath,data); } catch(Exception e) { hint.Text="Could not save garden: "+e.Message; } }
 void Export() { using(var dialog=new SaveFileDialog { Filter="Focus Garden (*.json)|*.json",FileName="focus-garden-"+DateTime.Today.ToString("yyyy-MM-dd")+".json" }) if(dialog.ShowDialog()==DialogResult.OK) try { using(var f=File.Create(dialog.FileName)) Serializer().WriteObject(f,data); hint.Text="Backup saved."; } catch(Exception e) { MessageBox.Show(e.Message,"Export failed"); } }
 void Import() { using(var dialog=new OpenFileDialog { Filter="Focus Garden (*.json)|*.json" }) if(dialog.ShowDialog()==DialogResult.OK) try { if(new FileInfo(dialog.FileName).Length>5000000) throw new Exception("The file exceeds 5 MB."); SaveData incoming; using(var f=File.OpenRead(dialog.FileName)) incoming=(SaveData)Serializer().ReadObject(f); data=MergeBackup(data,incoming); Save(); Render(); hint.Text="Garden and tasks imported. Existing local tasks were kept."; } catch(Exception e) { MessageBox.Show(e.Message,"Import failed"); } }
 void DrawGarden(object sender,PaintEventArgs e) {
  var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; plantHits.Clear();
  Color skyTop=data.Dark?Color.FromArgb(16,29,37):Color.FromArgb(223,239,239),skyBottom=data.Dark?Color.FromArgb(29,54,55):Color.FromArgb(192,221,206);
  using(var sky=new LinearGradientBrush(canvas.ClientRectangle,skyTop,skyBottom,90f)) g.FillRectangle(sky,canvas.ClientRectangle);
  for(int i=0;i<32;i++) { int x=20+(i*137)%510,y=12+(i*47)%92; int alpha=data.Dark?(int)(60+45*(1+Math.Sin(scenePhase+i))):50; using(var star=new SolidBrush(Color.FromArgb(alpha,205,234,221))) g.FillEllipse(star,x,y,i%4==0?2:1,i%4==0?2:1); }
  using(var glow=new SolidBrush(Color.FromArgb(18,196,233,218))) g.FillEllipse(glow,452,10,68,68);
  using(var moon=new SolidBrush(data.Dark?Color.FromArgb(213,234,221):Color.FromArgb(247,225,160))) g.FillEllipse(moon,470,26,32,32);
  if(data.Dark) using(var shade=new SolidBrush(skyTop)) g.FillEllipse(shade,462,20,30,30);
  using(var shadow=new SolidBrush(Color.FromArgb(30,0,0,0))) g.FillEllipse(shadow,103,177,358,28);
  Point[] top={new Point(61,111),new Point(315,50),new Point(510,127),new Point(254,195)};
  Point[] front={new Point(61,111),new Point(254,195),new Point(510,127),new Point(510,145),new Point(254,214),new Point(61,129)};
  using(var edge=new SolidBrush(data.Dark?Color.FromArgb(41,65,54):Color.FromArgb(110,154,120))) g.FillPolygon(edge,front);
  using(var soil=new LinearGradientBrush(new Rectangle(60,50,450,146),data.Dark?Color.FromArgb(72,109,76):Color.FromArgb(155,191,135),data.Dark?Color.FromArgb(48,81,65):Color.FromArgb(114,160,118),45f)) g.FillPolygon(soil,top);
  using(var rim=new Pen(Color.FromArgb(65,192,220,161))) g.DrawPolygon(rim,top);
  using(var patch=new SolidBrush(Color.FromArgb(22,207,232,167))) for(int i=0;i<12;i++) g.FillEllipse(patch,111+(i*67)%292,102+(i*17)%50,18,6);
  var visible=data.Plants.OrderBy(p=>p.Date).Skip(Math.Max(0,data.Plants.Count-18)).ToList();
  for(int i=0;i<visible.Count;i++) {
   int col=i%6,row=i/6,x=119+col*47+row*28,y=127+row*22-col*5;
   using(var shadow=new SolidBrush(Color.FromArgb(40,10,28,23))) g.FillEllipse(shadow,x-2,y,31,8);
   var state=g.Save(); g.TranslateTransform(x,y); g.ScaleTransform(.78f,.78f); DrawPlant(g,0,0,visible[i].Kind); g.Restore(state);
   plantHits.Add(Tuple.Create(new Rectangle(x,y-42,35,49),visible[i]));
  }
  if(visible.Count==0) { DrawPlant(g,258,136,data.SelectedKind<0?0:data.SelectedKind); }
  using(var brush=new SolidBrush(data.Dark?Color.FromArgb(160,190,184):Color.FromArgb(55,90,78))) using(var font=new Font("Segoe UI",8)) { string caption=visible.Count==0?"Complete a session to plant your first seed":"Click a plant to view its session"; var size=g.MeasureString(caption,font); g.DrawString(caption,font,brush,(550-size.Width)/2,213); }
 }
 void DrawDailyStats(object sender,PaintEventArgs e) {
  var g=e.Graphics; g.Clear(panel); int today=TodayMinutes,streak=CountStreak(data.Plants,DateTime.Today);
  using(var small=new Font("Segoe UI",8)) using(var big=new Font("Segoe UI",14)) using(var soft=new SolidBrush(muted)) using(var text=new SolidBrush(ink)) {
   g.DrawString(today>=data.DailyGoal?"GOAL COMPLETE":"TODAY",small,soft,0,0); g.DrawString(today+" / "+data.DailyGoal+" min",big,text,0,18);
   g.DrawString("DAY STREAK",small,soft,227,0); g.DrawString(streak.ToString(),big,text,227,18);
   g.DrawString("PLANTS",small,soft,414,0); g.DrawString(data.Plants.Count.ToString(),big,text,414,18);
  }
  using(var track=new SolidBrush(border)) using(var path=Shapes.Round(new Rectangle(0,56,502,4),2)) g.FillPath(track,path);
  int width=(int)(502*Math.Min(1,today/(double)data.DailyGoal)); if(width>3) using(var fill=new SolidBrush(accent)) using(var path=Shapes.Round(new Rectangle(0,56,width,4),2)) g.FillPath(fill,path);
 }
 void DrawPlant(Graphics g,int x,int y,int kind) {
  Fill(g,"#416F43",x+18,y-28,4,32); Fill(g,"#78A059",x+8,y-16,10,5); Fill(g,"#78A059",x+22,y-24,10,5);
  if(kind==1) { Fill(g,"#7C9E50",x+14,y-50,12,10); Fill(g,"#7C9E50",x+9,y-40,22,9); Fill(g,"#7C9E50",x+4,y-31,32,10); }
  else if(kind==3) { Fill(g,"#B57851",x+8,y-43,24,6); Fill(g,"#B57851",x+3,y-37,34,13); Fill(g,"#F3DFB3",x+10,y-35,5,5); Fill(g,"#F3DFB3",x+24,y-30,4,4); }
  else { string color=kind==0?"#E8BD64":"#C28CA1"; Fill(g,color,x+12,y-47,16,25); Fill(g,color,x+6,y-41,28,13); Fill(g,"#EFDC91",x+17,y-37,6,6); }
  Fill(g,"#839861",x+6,y+4,28,4);
 }
 void Fill(Graphics g,string color,int x,int y,int w,int h) { using(var b=new SolidBrush(ColorTranslator.FromHtml(color))) g.FillRectangle(b,x,y,w,h); }
}
public class Canvas : Panel { public Canvas() { DoubleBuffered=true; } }
public class Card : Panel { public Card() { DoubleBuffered=true; Resize+=delegate { if(Width<2 || Height<2) return; using(var path=Shapes.Round(ClientRectangle,20)) { var old=Region; Region=new Region(path); if(old!=null) old.Dispose(); } }; } }
public sealed class SessionList : Control {
 readonly List<Plant> entries=new List<Plant>();
 int offset,hover=-1;
 public Color Subtle=Color.Gray,Highlight=Color.LightGreen;
 public string EmptyMessage="Completed sessions will appear here.";
 public event Action<Plant> SessionSelected;
 public SessionList() { DoubleBuffered=true; TabStop=true; Font=new Font("Segoe UI",9); AccessibleName="Recent focus sessions"; }
 public void SetEntries(IEnumerable<Plant> value) { entries.Clear(); entries.AddRange(value); offset=0; hover=-1; Invalidate(); }
 protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int row=e.Y/27; if(row!=hover) { hover=row; Invalidate(); } Cursor=offset+row<entries.Count?Cursors.Hand:Cursors.Default; }
 protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover=-1; Invalidate(); }
 protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); offset=Math.Max(0,Math.Min(Math.Max(0,entries.Count-Math.Max(1,Height/27)),offset-Math.Sign(e.Delta)*3)); Invalidate(); }
 protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); int index=offset+e.Y/27; if(index>=0 && index<entries.Count && SessionSelected!=null) SessionSelected(entries[index]); }
 protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Up || keyData==Keys.Down || keyData==Keys.PageDown || keyData==Keys.PageUp || base.IsInputKey(keyData); }
 protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); int delta=e.KeyCode==Keys.Down?1:e.KeyCode==Keys.Up?-1:e.KeyCode==Keys.PageDown?3:e.KeyCode==Keys.PageUp?-3:0; if(delta!=0) { offset=Math.Max(0,Math.Min(Math.Max(0,entries.Count-Math.Max(1,Height/27)),offset+delta)); Invalidate(); e.Handled=true; } }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
  if(entries.Count==0) { TextRenderer.DrawText(g,EmptyMessage,Font,ClientRectangle,Subtle,TextFormatFlags.VerticalCenter); return; }
  int rows=Math.Max(1,Height/27);
  for(int i=0;i<rows && offset+i<entries.Count;i++) {
   var p=entries[offset+i]; int y=i*27;
   if(i==hover) using(var b=new SolidBrush(Color.FromArgb(24,Highlight))) using(var shape=Shapes.Round(new Rectangle(0,y,Width-12,26),8)) g.FillPath(b,shape);
   TextRenderer.DrawText(g,p.Date.ToLocalTime().ToString("MMM dd",System.Globalization.CultureInfo.InvariantCulture),Font,new Rectangle(8,y,67,26),Subtle,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
   TextRenderer.DrawText(g,String.IsNullOrWhiteSpace(p.Task)?"Untitled session":p.Task,Font,new Rectangle(84,y,Width-185,26),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   TextRenderer.DrawText(g,p.Minutes+" min",Font,new Rectangle(Width-91,y,70,26),Highlight,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.Right);
  }
  if(entries.Count>rows) { int h=Math.Max(12,Height*rows/entries.Count),y=offset*(Height-h)/(entries.Count-rows); using(var b=new SolidBrush(Subtle)) using(var shape=Shapes.Round(new Rectangle(Width-5,y,3,h),1)) g.FillPath(b,shape); }
 }
}
