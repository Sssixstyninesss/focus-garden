using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

[DataContract] public sealed class FocusTask {
 [DataMember] public string Id;
 [DataMember] public string Title;
 [DataMember] public DateTime Created;
 [DataMember] public bool Done;
 [DataMember] public bool Archived;
 [DataMember] public DateTime? CompletedAt;
}

public static class TaskStore {
 public static FocusTask Add(SaveData data,string title,DateTime now) {
  title=(title??"").Trim();
  if(title.Length==0 || title.Length>120) throw new ArgumentException("Enter a task name from 1 to 120 characters.");
  if(data.Tasks.Count(t=>!t.Archived)>=200 || data.Tasks.Count>=5000) throw new InvalidOperationException("The task list is full. Archive completed tasks before adding more.");
  var task=new FocusTask { Id=Guid.NewGuid().ToString("N"),Title=title,Created=now.ToUniversalTime() }; data.Tasks.Add(task); return task;
 }
 public static bool Select(SaveData data,string id) {
  if(data.Running) return false;
  var task=data.Tasks.FirstOrDefault(t=>t.Id==id && !t.Archived && !t.Done);
  if(task==null) return false;
  data.ActiveTaskId=task.Id; data.Task=task.Title; return true;
 }
 public static bool ToggleDone(SaveData data,string id,DateTime now) {
  if(data.Running && data.ActiveTaskId==id) return false;
  var task=data.Tasks.FirstOrDefault(t=>t.Id==id && !t.Archived); if(task==null) return false;
  task.Done=!task.Done; task.CompletedAt=task.Done?(DateTime?)now.ToUniversalTime():null;
  if(task.Done && data.ActiveTaskId==id) { data.ActiveTaskId=""; data.Task=""; }
  return true;
 }
 public static void ArchiveDone(SaveData data) { foreach(var task in data.Tasks.Where(t=>t.Done)) task.Archived=true; }
 public static bool Archive(SaveData data,string id) { if(data.Running && data.ActiveTaskId==id) return false; var task=data.Tasks.FirstOrDefault(t=>t.Id==id && !t.Archived); if(task==null) return false; task.Archived=true; if(data.ActiveTaskId==id) { data.ActiveTaskId=""; data.Task=""; } return true; }
 public static bool Restore(SaveData data,string id) { if(data.Tasks.Count(t=>!t.Archived)>=200) return false; var task=data.Tasks.FirstOrDefault(t=>t.Id==id && t.Archived); if(task==null) return false; task.Archived=false; return true; }
 public static void RenameActive(SaveData data,string title) {
  var task=data.Tasks.FirstOrDefault(t=>t.Id==data.ActiveTaskId && !t.Archived && !t.Done);
  if(task==null) { data.ActiveTaskId=""; return; }
  title=(title??"").Trim(); if(title.Length>120) throw new ArgumentException("Task names can contain up to 120 characters."); if(title.Length==0) data.ActiveTaskId=""; else task.Title=title;
 }
}

public sealed class WeekSummary {
 public readonly int[] Minutes=new int[7],Sessions=new int[7],Completed=new int[7];
 public DateTime Start;
 public int TotalMinutes { get { return Minutes.Sum(); } }
 public int TotalSessions { get { return Sessions.Sum(); } }
 public int TotalCompleted { get { return Completed.Sum(); } }
 public int ActiveDays { get { return Minutes.Count(m=>m>0); } }
 public string ToCsv() { var text=new System.Text.StringBuilder("Day,FocusMinutes,Sessions,CompletedTasks\r\n"); for(int day=0;day<7;day++) text.Append(Start.AddDays(day).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(Minutes[day]).Append(',').Append(Sessions[day]).Append(',').Append(Completed[day]).Append("\r\n"); return text.ToString(); }
 public static WeekSummary Create(SaveData data,DateTime today) {
  var week=new WeekSummary { Start=today.Date.AddDays(-6) };
  foreach(var p in data.Plants) { int day=(p.Date.ToLocalTime().Date-week.Start).Days; if(day>=0 && day<7) { week.Minutes[day]+=p.Minutes; week.Sessions[day]++; } }
  foreach(var task in data.Tasks.Where(t=>t.Done && t.CompletedAt.HasValue)) { int day=(task.CompletedAt.Value.ToLocalTime().Date-week.Start).Days; if(day>=0 && day<7) week.Completed[day]++; }
  return week;
 }
}

public sealed class TaskBoard : Control {
 readonly List<FocusTask> tasks=new List<FocusTask>();
 int offset,hover=-1,keyboardIndex;
 string activeId="";
 public Color Muted=Color.Gray,Accent=Color.LightGreen;
 public event Action<string> Selected,CompletionRequested,ArchiveRequested;
 public bool ShowArchived;
 public TaskBoard() { DoubleBuffered=true; TabStop=true; Font=new Font("Segoe UI",10); AccessibleName="Task queue"; }
 public void RefreshTasks(SaveData data) {
  string cursorId=keyboardIndex>=0 && keyboardIndex<tasks.Count?tasks[keyboardIndex].Id:null;
  tasks.Clear(); tasks.AddRange(data.Tasks.Where(t=>t.Archived==ShowArchived).OrderBy(t=>t.Done).ThenBy(t=>t.Created)); activeId=data.ActiveTaskId;
  int retained=tasks.FindIndex(t=>t.Id==cursorId); keyboardIndex=retained>=0?retained:Math.Max(0,Math.Min(keyboardIndex,tasks.Count-1));
  offset=Math.Min(offset,Math.Max(0,tasks.Count-Math.Max(1,Height/52)));
  AccessibleDescription=String.Join("; ",tasks.Select(t=>(t.Done?"Completed: ":"Pending: ")+t.Title)); Invalidate();
 }
 protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int row=e.Y/52; if(hover!=row) { hover=row; Invalidate(); } Cursor=row+offset<tasks.Count?Cursors.Hand:Cursors.Default; }
 protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover=-1; Invalidate(); }
 protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button!=MouseButtons.Left) return; int index=offset+e.Y/52; if(index<0 || index>=tasks.Count) return; keyboardIndex=index; Focus(); if(e.X>Width-44) { if(ArchiveRequested!=null) ArchiveRequested(tasks[index].Id); } else if(e.X<38 && !tasks[index].Archived) { if(CompletionRequested!=null) CompletionRequested(tasks[index].Id); } else if(!tasks[index].Done && !tasks[index].Archived && Selected!=null) Selected(tasks[index].Id); Invalidate(); }
 protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); ScrollBy(-Math.Sign(e.Delta)*2); }
 void ScrollBy(int rows) { int visible=Math.Max(1,Height/52); offset=Math.Max(0,Math.Min(Math.Max(0,tasks.Count-visible),offset+rows)); keyboardIndex=Math.Max(offset,Math.Min(keyboardIndex,offset+visible-1)); Invalidate(); }
 protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Up || keyData==Keys.Down || keyData==Keys.Space || keyData==Keys.Enter || keyData==Keys.Delete || base.IsInputKey(keyData); }
 protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
 protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
 protected override void OnKeyDown(KeyEventArgs e) {
  base.OnKeyDown(e); if(tasks.Count==0) return;
  int delta=e.KeyCode==Keys.Down?1:e.KeyCode==Keys.Up?-1:0;
  if(delta!=0) { keyboardIndex=Math.Max(0,Math.Min(tasks.Count-1,keyboardIndex+delta)); int rows=Math.Max(1,Height/52); if(keyboardIndex<offset) offset=keyboardIndex; if(keyboardIndex>=offset+rows) offset=keyboardIndex-rows+1; hover=-1; Invalidate(); e.SuppressKeyPress=true; }
  if(e.KeyCode==Keys.Space && !tasks[keyboardIndex].Archived && CompletionRequested!=null) { CompletionRequested(tasks[keyboardIndex].Id); e.SuppressKeyPress=true; }
  if(e.KeyCode==Keys.Enter && !tasks[keyboardIndex].Done && !tasks[keyboardIndex].Archived && Selected!=null) { Selected(tasks[keyboardIndex].Id); e.SuppressKeyPress=true; }
  if(e.KeyCode==Keys.Delete && ArchiveRequested!=null) { ArchiveRequested(tasks[keyboardIndex].Id); e.SuppressKeyPress=true; }
 }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
  if(tasks.Count==0) { using(var big=new Font("Segoe UI",15,FontStyle.Bold)) TextRenderer.DrawText(g,ShowArchived?"No archived tasks":"Nothing queued yet",big,new Rectangle(10,48,Width-20,34),ForeColor,TextFormatFlags.HorizontalCenter); TextRenderer.DrawText(g,ShowArchived?"Archived tasks can be restored here.":"Add a task above to plan your next session.",Font,new Rectangle(10,92,Width-20,40),Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak); return; }
  int rows=Math.Max(1,Height/52);
  for(int row=0;row<rows && offset+row<tasks.Count;row++) {
   var task=tasks[offset+row]; int y=row*52; bool selected=task.Id==activeId;
   using(var fill=new SolidBrush(Color.FromArgb(selected?35:row==hover || (Focused && offset+row==keyboardIndex)?22:10,Accent))) using(var shape=Shapes.Round(new Rectangle(0,y,Width-10,47),12)) g.FillPath(fill,shape);
   if(Focused && offset+row==keyboardIndex) using(var line=new Pen(Color.FromArgb(90,Accent))) using(var shape=Shapes.Round(new Rectangle(1,y+1,Width-13,44),11)) g.DrawPath(line,shape);
   using(var pen=new Pen(task.Done?Accent:Muted,1.4f)) g.DrawEllipse(pen,12,y+15,16,16);
   if(task.Done) using(var pen=new Pen(Accent,1.5f)) g.DrawLines(pen,new[]{new Point(16,y+23),new Point(19,y+26),new Point(25,y+19)});
   using(var nameFont=new Font("Segoe UI",10,task.Done?FontStyle.Strikeout:FontStyle.Regular)) TextRenderer.DrawText(g,task.Title,nameFont,new Rectangle(42,y+6,Width-169,23),task.Done?Muted:ForeColor,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   using(var small=new Font("Segoe UI",8)) TextRenderer.DrawText(g,task.Archived?"Archived · use ↶ to restore":task.Done?"Completed":selected?"Selected for focus":"Click to focus",small,new Rectangle(43,y+27,Width-135,16),Muted,TextFormatFlags.SingleLine);
   if(!task.Done && !task.Archived) TextRenderer.DrawText(g,selected?"Active":"Focus",Font,new Rectangle(Width-117,y+12,63,26),selected?Accent:Muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
   TextRenderer.DrawText(g,task.Archived?"↶":"×",Font,new Rectangle(Width-40,y+12,25,26),Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
  if(tasks.Count>rows) { int h=Math.Max(12,Height*rows/tasks.Count),y=offset*(Height-h)/(tasks.Count-rows); using(var brush=new SolidBrush(Muted)) g.FillRectangle(brush,Width-4,y,2,h); }
 }
}

public sealed class WeekView : Control {
 SaveData data;
 WeekSummary summary;
 int hover=-1;
 public Color Muted=Color.Gray,Accent=Color.LightGreen;
 public event Action ExportRequested;
 public event Action<DateTime> DaySelected;
 public WeekView() { DoubleBuffered=true; Font=new Font("Segoe UI",10); AccessibleName="Last seven days"; var export=new PillButton { UseControlColors=true,Text="Export CSV",Font=new Font("Segoe UI",9),Location=new Point(374,4),Size=new Size(128,28) }; export.Click+=delegate { if(ExportRequested!=null) ExportRequested(); }; Controls.Add(export); }
 public void RefreshData(SaveData value) { data=value; summary=WeekSummary.Create(data,DateTime.Today); AccessibleDescription=summary.TotalMinutes+" focus minutes, "+summary.TotalSessions+" sessions and "+summary.TotalCompleted+" completed tasks in the last seven days."; Invalidate(); }
 protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int index=(e.X-20)/66; index=e.Y>=144 && e.Y<=310 && e.X>=20 && index<7?index:-1; if(index!=hover) { hover=index; Invalidate(); } Cursor=hover>=0?Cursors.Hand:Cursors.Default; }
 protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover=-1; Cursor=Cursors.Default; Invalidate(); }
 protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button!=MouseButtons.Left) return; int day=(e.X-20)/66; if(summary!=null && e.X>=20 && day>=0 && day<7 && e.Y>=144 && e.Y<=310 && DaySelected!=null) DaySelected(summary.Start.AddDays(day)); }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); if(summary==null) return; var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var title=new Font("Segoe UI",22,FontStyle.Bold)) TextRenderer.DrawText(g,"Your week",title,new Rectangle(0,0,Width,43),ForeColor);
  using(var small=new Font("Segoe UI",8)) TextRenderer.DrawText(g,summary.Start.ToString("MMM dd",System.Globalization.CultureInfo.InvariantCulture)+" – "+summary.Start.AddDays(6).ToString("MMM dd",System.Globalization.CultureInfo.InvariantCulture),small,new Rectangle(3,44,Width,23),Muted);
  string[] values={summary.TotalMinutes+" min",summary.TotalSessions.ToString(),summary.TotalCompleted.ToString()}; string[] labels={"FOCUS","SESSIONS","TASKS DONE"};
  for(int i=0;i<3;i++) { using(var big=new Font("Segoe UI",20)) TextRenderer.DrawText(g,values[i],big,new Rectangle(i*170,76,165,37),i==0?Accent:ForeColor); using(var small=new Font("Segoe UI",8)) TextRenderer.DrawText(g,labels[i],small,new Rectangle(i*170+3,114,165,20),Muted); }
  int max=Math.Max(data.DailyGoal,summary.Minutes.Max());
  for(int day=0;day<7;day++) {
   int x=24+day*66,h=(int)(140*summary.Minutes[day]/(double)Math.Max(1,max));
   using(var track=new SolidBrush(Color.FromArgb(16,Accent))) using(var shape=Shapes.Round(new Rectangle(x,151,40,145),10)) g.FillPath(track,shape);
   if(h>0) using(var fill=new SolidBrush(day==6 || day==hover?Accent:Color.FromArgb(150,Accent))) using(var shape=Shapes.Round(new Rectangle(x,296-Math.Max(4,h),40,Math.Max(4,h)),Math.Min(10,Math.Max(2,h/2)))) g.FillPath(fill,shape);
   using(var small=new Font("Segoe UI",8)) { TextRenderer.DrawText(g,summary.Start.AddDays(day).ToString("ddd",System.Globalization.CultureInfo.InvariantCulture),small,new Rectangle(x-6,306,52,22),day==6?Accent:Muted,TextFormatFlags.HorizontalCenter); if(day==hover) TextRenderer.DrawText(g,summary.Minutes[day]+"m",small,new Rectangle(x-8,130,56,20),Accent,TextFormatFlags.HorizontalCenter); }
  }
  using(var pen=new Pen(Color.FromArgb(110,Accent))) { pen.DashStyle=DashStyle.Dash; float y=296-140*data.DailyGoal/(float)max; g.DrawLine(pen,18,y,474,y); }
  using(var small=new Font("Segoe UI",8)) TextRenderer.DrawText(g,summary.ActiveDays+" active days  ·  Dashed line: daily goal",small,new Rectangle(3,339,Width,23),Muted);
 }
}

public sealed class CueTextBox : TextBox {
 public string Cue="";
 protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
 protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
 protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
 protected override void WndProc(ref Message message) {
  base.WndProc(ref message);
  if((message.Msg==0xF || message.Msg==0x317 || message.Msg==0x318) && Text.Length==0 && !Focused && !String.IsNullOrEmpty(Cue)) {
   bool printing=message.Msg!=0xF && message.WParam!=IntPtr.Zero;
   using(var graphics=printing?Graphics.FromHdc(message.WParam):Graphics.FromHwnd(Handle)) TextRenderer.DrawText(graphics,Cue,Font,new Rectangle(2,0,Math.Max(1,ClientSize.Width-4),ClientSize.Height),BackColor.GetBrightness()>.5?Color.FromArgb(88,107,116):Color.FromArgb(155,174,181),TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  }
 }
}
