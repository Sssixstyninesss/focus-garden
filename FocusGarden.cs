using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

[DataContract] public class Plant {
 [DataMember] public string Task;
 [DataMember] public int Minutes;
 [DataMember] public DateTime Date;
 [DataMember] public int Kind;
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
}
public class Garden : Form {
 SaveData data = new SaveData();
 string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "garden.json");
 Label clock = new Label(), hint = new Label(), stats = new Label(), title = new Label(), tagline = new Label(), taskLabel = new Label(), gardenLabel = new Label();
 TextBox task = new TextBox(); ListBox history = new ListBox();
 Button start, reset, theme, mode, export, import;
 List<Button> durations = new List<Button>();
 Panel timerPanel = new Card(), gardenPanel = new Card();
 Canvas canvas = new Canvas();
 System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
 bool loading = true, sound = true; Button soundButton;
 Color bg, panel, ink, muted, border, accent;
 FocusIsland island;
 NotifyIcon tray;
 bool exiting, previewMode;
 Panel windowHeader;
 Button minimizeButton, closeButton;
 [STAThread] public static void Main(string[] args) {
  if (args.Contains("--self-test")) { SelfTest(); return; }
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  if (args.Contains("--render-preview")) { RenderPreview(args.Last()); return; }
  using (var mutex = new System.Threading.Mutex(true,"Local\\FocusGardenDesktop",out single)) { if (!single) { MessageBox.Show("Focus Garden is already running."); return; } Application.Run(new Garden()); }
 }
 static bool single;
 static void SelfTest() {
  var d = new SaveData(); d.Plants.Add(new Plant { Task = "Test", Minutes = 25, Date = DateTime.UtcNow, Kind = 2 });
  using(var ms = new MemoryStream()) { Serializer().WriteObject(ms,d); ms.Position=0; var copy=(SaveData)Serializer().ReadObject(ms); Validate(copy); if(copy.Plants[0].Task!="Test" || !copy.Dark) throw new Exception("Serialization failed"); }
  if (Format(65)!="01:05" || Seconds(DateTime.UtcNow.AddSeconds(-2))!=0) throw new Exception("Timer failed");
  bool rejected=false; try { Validate(new SaveData { Minutes=-1 }); } catch { rejected=true; } if(!rejected) throw new Exception("Invalid duration accepted");
  var presence=new IslandPresence(); presence.Wake(0);
  if(presence.Update(1,false,false,false) || presence.Sleeping) throw new Exception("Hidden too early");
  if(!presence.Update(2,false,false,false) || !presence.Sleeping) throw new Exception("Auto-hide failed");
  if(presence.Update(2.1,false,true,false)) throw new Exception("Hover delay missing");
  if(!presence.Update(2.3,false,true,false) || presence.Sleeping) throw new Exception("Hover reveal failed");
  presence.Pinned=true; presence.Update(20,false,false,false); if(presence.Sleeping) throw new Exception("Pin failed");
  presence.Pinned=false; presence.Enabled=false; presence.Update(50,false,true,false); if(presence.Sleeping) throw new Exception("Suspended island changed state");
  Console.WriteLine("PASS: persistence, validation, timer, auto-hide, hover reveal, pin, suspension");
 }
 static DataContractJsonSerializer Serializer() { return new DataContractJsonSerializer(typeof(SaveData)); }
 static void Validate(SaveData d) {
  if(d==null || d.Plants==null || d.Plants.Count>10000 || d.Minutes<1 || d.Minutes>120 || double.IsNaN(d.Remaining) || double.IsInfinity(d.Remaining) || d.Remaining<0 || d.Remaining>7200 || (d.Running && d.Deadline==default(DateTime))) throw new Exception("Invalid garden file.");
  foreach(var p in d.Plants) if(p==null || p.Task==null || p.Task.Length>120 || p.Minutes<1 || p.Minutes>120 || p.Kind<0 || p.Kind>3) throw new Exception("Invalid plant data.");
 }
 static string Format(double seconds) { int s=(int)Math.Ceiling(seconds); return (s/60).ToString("00")+":"+(s%60).ToString("00"); }
 static double Seconds(DateTime deadline) { return Math.Max(0,(deadline-DateTime.UtcNow).TotalSeconds); }
 public Garden() : this(false) { }
 public Garden(bool preview) {
  previewMode=preview;
  Text="Focus Garden"; Icon=LoadIcon(); FormBorderStyle=FormBorderStyle.None; ClientSize=new Size(1060,792); MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); DoubleBuffered=true;
  try { if(!previewMode && File.Exists(savePath)) using(var f=File.OpenRead(savePath)) { data=(SaveData)Serializer().ReadObject(f); Validate(data); } } catch { data=new SaveData(); MessageBox.Show("The saved garden could not be read. The original file will be backed up on the next save.","Focus Garden"); }
  title=LabelAt(this,"Focus Garden",82,22,400,46,24,FontStyle.Bold);
  var mark=new PictureBox { Image=LoadMark(),SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(34,28),Size=new Size(36,36) }; Controls.Add(mark);
  tagline=LabelAt(this,"DESKTOP FOCUS TIMER",34,78,600,28,10,FontStyle.Regular);
  theme=ButtonAt(this,"Light theme",820,28,200,38,delegate { data.Dark=!data.Dark; ApplyTheme(); Save(); });
  soundButton=ButtonAt(this,"Sound: on",820,76,200,32,delegate { sound=!sound; soundButton.Text=sound?"Sound: on":"Sound: off"; });
  ButtonAt(this,"Focus Island",610,28,190,38,ShowIsland);
  timerPanel.SetBounds(30,130,430,430); Controls.Add(timerPanel);
  gardenPanel.SetBounds(480,130,550,430); Controls.Add(gardenPanel);
  LabelAt(timerPanel,"FOCUS",24,20,370,24,10,FontStyle.Regular);
  mode=ButtonAt(timerPanel,"Focus · switch to break",24,58,382,36,delegate { if(data.Running) { hint.Text="Pause the timer before switching modes."; return; } data.Rest=!data.Rest; SetDuration(data.Rest?5:25); });
  clock=LabelAt(timerPanel,"25:00",20,102,390,98,64,FontStyle.Regular); clock.TextAlign=ContentAlignment.MiddleCenter;
  int x=24; foreach(int value in new[]{15,25,50,1}) { int minutes=value; var b=ButtonAt(timerPanel,value==1?"1m demo":value+" min",x,214,88,34,delegate { if(!data.Running) SetDuration(minutes); }); durations.Add(b); x+=98; }
  taskLabel=LabelAt(timerPanel,"What are you working on?",24,266,380,24,10,FontStyle.Regular);
  task.SetBounds(24,295,382,30); task.MaxLength=120; task.Text=data.Task??""; timerPanel.Controls.Add(task);
  task.BorderStyle=BorderStyle.FixedSingle;
  task.TextChanged+=delegate { data.Task=task.Text; if(!loading) Save(); };
  start=ButtonAt(timerPanel,"Start focus",24,350,280,48,Toggle);
  reset=ButtonAt(timerPanel,"Reset",314,350,92,48,delegate { data.Running=false; data.Remaining=data.Minutes*60; Save(); Render(); });
  gardenLabel=LabelAt(gardenPanel,"YOUR GARDEN",24,20,490,28,10,FontStyle.Regular);
  LabelAt(gardenPanel,"Your session garden",24,64,500,42,20,FontStyle.Bold);
  canvas.SetBounds(0,120,550,230); gardenPanel.Controls.Add(canvas); canvas.Paint+=DrawGarden;
  stats=LabelAt(gardenPanel,"",20,365,510,42,15,FontStyle.Regular); stats.TextAlign=ContentAlignment.MiddleCenter;
  hint=LabelAt(this,"Your garden is saved on this device.",32,570,990,28,10,FontStyle.Regular); hint.AccessibleRole=AccessibleRole.StaticText;
  LabelAt(this,"Recent sessions",32,610,400,28,16,FontStyle.Bold);
  history.SetBounds(32,648,698,82); history.BorderStyle=BorderStyle.None; history.HorizontalScrollbar=true; Controls.Add(history);
  export=ButtonAt(this,"Export garden",760,640,270,38,Export);
  import=ButtonAt(this,"Import garden",760,688,270,38,Import);
  foreach(Control control in Controls) control.Top+=32;
  windowHeader=new Panel { Location=Point.Empty,Size=new Size(1060,32) }; Controls.Add(windowHeader);
  var windowName=LabelAt(windowHeader,"Focus Garden",26,4,300,24,9,FontStyle.Regular);
  bool moving=false; Point mouseOrigin=Point.Empty,windowOrigin=Point.Empty;
  MouseEventHandler down=delegate(object sender,MouseEventArgs e) { if(e.Button!=MouseButtons.Left) return; moving=true; mouseOrigin=Cursor.Position; windowOrigin=Location; ((Control)sender).Capture=true; };
  MouseEventHandler move=delegate { if(moving) Location=new Point(windowOrigin.X+Cursor.Position.X-mouseOrigin.X,windowOrigin.Y+Cursor.Position.Y-mouseOrigin.Y); };
  MouseEventHandler up=delegate(object sender,MouseEventArgs e) { moving=false; ((Control)sender).Capture=false; };
  windowHeader.MouseDown+=down; windowHeader.MouseMove+=move; windowHeader.MouseUp+=up;
  windowName.MouseDown+=down; windowName.MouseMove+=move; windowName.MouseUp+=up;
  minimizeButton=ButtonAt(windowHeader,"−",958,4,40,24,delegate { WindowState=FormWindowState.Minimized; }); minimizeButton.AccessibleName="Minimize window";
  closeButton=ButtonAt(windowHeader,"×",1006,4,40,24,delegate { Close(); }); closeButton.AccessibleName="Close application";
  ((PillButton)closeButton).Destructive=true;
  using(var shape=Shapes.Round(ClientRectangle,22)) Region=new Region(shape);
  Paint+=delegate(object sender,PaintEventArgs e) { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var outline=new Pen(border)) using(var shape=Shapes.Round(new Rectangle(0,0,Width-1,Height-1),22)) e.Graphics.DrawPath(outline,shape); };
  Resize+=delegate { int extra=ClientSize.Width-1060; theme.Left=820+extra; soundButton.Left=820+extra; };
  FormClosing+=delegate { exiting=true; if(data.Running) data.Remaining=Seconds(data.Deadline); Save(); tick.Stop(); if(island!=null) { island.Close(); island.Dispose(); } if(tray!=null) { tray.Visible=false; tray.Dispose(); } };
  loading=false; ApplyTheme(); Render(); tick.Interval=250; tick.Tick+=delegate { UpdateTimer(); }; tick.Start(); UpdateTimer();
  if(!previewMode) {
   var menu=new ContextMenuStrip(); menu.Items.Add("Show island",null,delegate { ShowIsland(); }); menu.Items.Add("Open garden",null,delegate { ShowGarden(); }); menu.Items.Add("Quit",null,delegate { Close(); });
   tray=new NotifyIcon { Icon=Icon,Text="Focus Garden",ContextMenuStrip=menu,Visible=true };
   tray.DoubleClick+=delegate { ShowGarden(); };
   Shown+=delegate { ShowIsland(); };
  }
 }
 public SaveData State { get { return data; } }
 public string TimeText { get { return Format(data.Running?Seconds(data.Deadline):data.Remaining); } }
 public double Progress { get { return Math.Max(0,Math.Min(1,1-(data.Running?Seconds(data.Deadline):data.Remaining)/(data.Minutes*60.0))); } }
 public void ToggleFromIsland() { UpdateTimer(); Toggle(); }
 public void IslandDuration(int minutes) { if(!data.Running) SetDuration(minutes); }
 public void IslandBreak() { if(data.Running) return; data.Rest=!data.Rest; SetDuration(data.Rest?5:25); }
 public void ShowGarden() { if(island!=null) island.Suspend(); Show(); WindowState=FormWindowState.Normal; Activate(); }
 public void ShowIsland() { if(exiting) return; if(island==null || island.IsDisposed) island=new FocusIsland(this); island.Reveal(); Hide(); }
 public static Icon LoadIcon() { using(var stream=typeof(Garden).Assembly.GetManifestResourceStream("FocusGarden.Icon")) { if(stream==null) return (Icon)SystemIcons.Application.Clone(); using(var icon=new Icon(stream,new Size(32,32))) return (Icon)icon.Clone(); } }
 public static Image LoadMark() { using(var stream=typeof(Garden).Assembly.GetManifestResourceStream("FocusGarden.Mark")) { if(stream==null) return SystemIcons.Application.ToBitmap(); using(var image=Image.FromStream(stream)) return new Bitmap(image); } }
 public void ExitFromIsland() { Close(); }
 static void RenderPreview(string folder) {
  Directory.CreateDirectory(folder);
  using(var garden=new Garden(true)) {
   garden.data.Task="Finish the desktop app"; garden.task.Text=garden.data.Task;
   garden.data.Plants.Add(new Plant { Task="Read a chapter",Minutes=25,Date=DateTime.UtcNow,Kind=2 });
   garden.data.Remaining=18*60+42; garden.Render();
   using(var capsule=new FocusIsland(garden)) {
    capsule.CheckLifecycle();
    capsule.CreateControl(); capsule.RenderTo(Path.Combine(folder,"island-compact-en.png"),false);
    capsule.RenderTo(Path.Combine(folder,"island-expanded-en.png"),true);
    garden.ToggleFromIsland(); if(!garden.data.Running) throw new Exception("Island start failed");
    garden.ToggleFromIsland(); if(garden.data.Running || garden.data.Remaining>1123) throw new Exception("Island pause failed");
    garden.IslandBreak(); if(!garden.data.Rest || garden.data.Minutes!=5) throw new Exception("Island break failed");
    garden.IslandBreak(); garden.data.Deadline=DateTime.UtcNow.AddSeconds(-2); garden.data.Running=true; garden.UpdateTimer();
    if(garden.data.Running || garden.data.Plants.Count!=2) throw new Exception("Completed session did not grow a plant");
    garden.UpdateTimer(); if(garden.data.Plants.Count!=2) throw new Exception("Duplicate plant after completion");
    capsule.Celebrate(); capsule.RenderTo(Path.Combine(folder,"island-celebration-en.png"),true);
   }
   garden.data.Remaining=25*60; garden.Render();
   garden.StartPosition=FormStartPosition.Manual; garden.Location=new Point(-30000,-30000); garden.ShowInTaskbar=false; garden.Show();
   using(var bmp=new Bitmap(garden.Width,garden.Height)) { garden.DrawToBitmap(bmp,new Rectangle(0,0,garden.Width,garden.Height)); bmp.Save(Path.Combine(folder,"dark-theme-en.png")); }
   garden.data.Dark=false; garden.ApplyTheme();
   using(var bmp=new Bitmap(garden.Width,garden.Height)) { garden.DrawToBitmap(bmp,new Rectangle(0,0,garden.Width,garden.Height)); bmp.Save(Path.Combine(folder,"light-theme-en.png")); }
   garden.Hide();
  }
 }
 Label LabelAt(Control parent,string text,int x,int y,int w,int h,int size,FontStyle style) { var l=new Label { Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",size,style),BackColor=Color.Transparent }; parent.Controls.Add(l); return l; }
 Button ButtonAt(Control parent,string text,int x,int y,int w,int h,Action action) { var b=new PillButton { UseControlColors=true,Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",10),TabStop=true }; b.Click+=delegate { action(); }; parent.Controls.Add(b); return b; }
 void ApplyTheme() {
  bg=ColorTranslator.FromHtml(data.Dark?"#101418":"#EFF2F3"); panel=ColorTranslator.FromHtml(data.Dark?"#192126":"#FFFFFF"); ink=ColorTranslator.FromHtml(data.Dark?"#EBF0F1":"#1C2A2E"); muted=ColorTranslator.FromHtml(data.Dark?"#95A8AF":"#65777D"); border=ColorTranslator.FromHtml(data.Dark?"#29363C":"#E3EBEB"); accent=ColorTranslator.FromHtml(data.Dark?"#A7E3BC":"#275C48");
  BackColor=bg; PaintControls(this); timerPanel.BackColor=panel; gardenPanel.BackColor=panel; canvas.BackColor=panel;
  windowHeader.BackColor=bg; windowHeader.Controls[0].ForeColor=muted; minimizeButton.BackColor=bg; closeButton.BackColor=bg;
  theme.Text=data.Dark?"Light theme":"Dark theme"; start.BackColor=accent; start.ForeColor=data.Dark?bg:Color.White; hint.ForeColor=muted; tagline.ForeColor=muted; stats.ForeColor=muted; canvas.Invalidate();
 }
 void PaintControls(Control root) { foreach(Control c in root.Controls) { c.ForeColor=ink; if(c is Button) c.BackColor=border; else if(c is TextBox || c is ListBox) c.BackColor=panel; PaintControls(c); } }
 void SetDuration(int minutes) { data.Minutes=minutes; data.Remaining=minutes*60; data.Running=false; Save(); Render(); }
 void Toggle() { if(data.Running) { data.Remaining=Seconds(data.Deadline); data.Running=false; hint.Text="Session paused."; } else { data.Deadline=DateTime.UtcNow.AddSeconds(data.Remaining); data.Running=true; hint.Text="Session in progress."; } Save(); Render(); }
 void UpdateTimer() {
  if(data.Running) {
   data.Remaining=Seconds(data.Deadline);
   if(data.Remaining<=0) { data.Running=false; if(!data.Rest) { data.Plants.Add(new Plant { Task=task.Text.Trim(),Minutes=data.Minutes,Date=DateTime.UtcNow,Kind=new Random().Next(4) }); hint.Text="Session complete. A new plant has been added."; } else hint.Text="Break complete."; data.Remaining=data.Minutes*60; Save(); if(sound) System.Media.SystemSounds.Asterisk.Play(); if(island!=null) island.Celebrate(!data.Rest); Render(); }
  }
  clock.Text=Format(data.Remaining); Text=data.Running?clock.Text+" · Focus Garden":"Focus Garden";
 }
 void Render() {
  clock.Text=Format(data.Remaining); start.Text=data.Running?"Pause session":data.Remaining<data.Minutes*60?"Resume session":data.Rest?"Start break":"Start focus";
  mode.Text=data.Rest?"Break · switch to focus":"Focus · switch to break";
  foreach(var b in durations) { b.Enabled=!data.Running; b.ForeColor=ink; b.BackColor=border; }
  stats.Text="Plants: "+data.Plants.Count+"     /     Focus: "+data.Plants.Sum(p=>p.Minutes)+" min     /     Today: "+data.Plants.Count(p=>p.Date.ToLocalTime().Date==DateTime.Today);
  history.Items.Clear(); foreach(var p in data.Plants.AsEnumerable().Reverse().Take(30)) history.Items.Add(p.Date.ToLocalTime().ToString("MMM dd, HH:mm",System.Globalization.CultureInfo.InvariantCulture)+"     "+(p.Task.Length==0?"Untitled session":p.Task)+"     +"+p.Minutes+" min"); if(data.Plants.Count==0) history.Items.Add("Completed sessions will appear here."); canvas.Invalidate();
 }
 void Save() { if(previewMode) return; try { Directory.CreateDirectory(Path.GetDirectoryName(savePath)); string tmp=savePath+".tmp"; using(var f=File.Create(tmp)) Serializer().WriteObject(f,data); if(File.Exists(savePath)) File.Replace(tmp,savePath,savePath+".bak"); else File.Move(tmp,savePath); } catch(Exception e) { hint.Text="Could not save garden: "+e.Message; } }
 void Export() { using(var dialog=new SaveFileDialog { Filter="Focus Garden (*.json)|*.json",FileName="focus-garden-"+DateTime.Today.ToString("yyyy-MM-dd")+".json" }) if(dialog.ShowDialog()==DialogResult.OK) try { using(var f=File.Create(dialog.FileName)) Serializer().WriteObject(f,data); hint.Text="Backup saved."; } catch(Exception e) { MessageBox.Show(e.Message,"Export failed"); } }
 void Import() { using(var dialog=new OpenFileDialog { Filter="Focus Garden (*.json)|*.json" }) if(dialog.ShowDialog()==DialogResult.OK) try { if(new FileInfo(dialog.FileName).Length>5000000) throw new Exception("The file exceeds 5 MB."); SaveData incoming; using(var f=File.OpenRead(dialog.FileName)) incoming=(SaveData)Serializer().ReadObject(f); Validate(incoming); var merged=data.Plants.Concat(incoming.Plants).GroupBy(p=>p.Date.ToString("o")+"|"+p.Task+"|"+p.Minutes).Select(g=>g.First()).OrderBy(p=>p.Date).ToList(); if(merged.Count>10000) throw new Exception("A garden can contain up to 10,000 plants."); data.Plants=merged; Save(); Render(); hint.Text="Garden imported. Duplicate sessions were merged."; } catch(Exception e) { MessageBox.Show(e.Message,"Import failed"); } }
 void DrawGarden(object sender,PaintEventArgs e) {
  Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.None;
  using(var sky=new LinearGradientBrush(canvas.ClientRectangle,data.Dark?Color.FromArgb(24,43,36):Color.FromArgb(239,242,230),data.Dark?Color.FromArgb(43,66,46):Color.FromArgb(219,232,190),90f)) g.FillRectangle(sky,canvas.ClientRectangle);
  using(var sun=new SolidBrush(data.Dark?Color.FromArgb(222,231,190):Color.FromArgb(232,207,135))) g.FillEllipse(sun,447,22,42,42);
  using(var hill=new SolidBrush(data.Dark?Color.FromArgb(45,69,47):Color.FromArgb(197,211,172))) g.FillEllipse(hill,-100,110,600,230);
  using(var hill=new SolidBrush(data.Dark?Color.FromArgb(56,81,49):Color.FromArgb(179,197,149))) g.FillEllipse(hill,180,140,550,200);
  var visible=data.Plants.Skip(Math.Max(0,data.Plants.Count-18)).ToList();
  for(int i=0;i<visible.Count;i++) { int x=30+(i%9)*55,y=visible.Count<=9?170:125+(i/9)*60; DrawPlant(g,x,y,visible[i].Kind); }
  using(var brush=new SolidBrush(data.Dark?Color.FromArgb(195,210,170):Color.FromArgb(90,117,66))) using(var font=new Font("Segoe UI",9)) { string caption=visible.Count==0?"Complete a focus session to grow your first plant":"One plant for every completed session"; var size=g.MeasureString(caption,font); g.DrawString(caption,font,brush,(550-size.Width)/2,210); }
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
