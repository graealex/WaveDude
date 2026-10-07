using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.VisualBasic.CompilerServices;

namespace _2D_wave_sim_V01.My
{
	// Token: 0x02000004 RID: 4
	[StandardModule]
	[HideModuleName]
	[GeneratedCode("MyTemplate", "11.0.0.0")]
	internal sealed class MyProject
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000006 RID: 6 RVA: 0x0000210C File Offset: 0x0000030C
		[HelpKeyword("My.Computer")]
		internal static MyComputer Computer
		{
			[DebuggerHidden]
			get
			{
				return MyProject.m_ComputerObjectProvider.GetInstance;
			}
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000007 RID: 7 RVA: 0x00002128 File Offset: 0x00000328
		[HelpKeyword("My.Application")]
		internal static MyApplication Application
		{
			[DebuggerHidden]
			get
			{
				return MyProject.m_AppObjectProvider.GetInstance;
			}
		}

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000008 RID: 8 RVA: 0x00002144 File Offset: 0x00000344
		[HelpKeyword("My.User")]
		internal static User User
		{
			[DebuggerHidden]
			get
			{
				return MyProject.m_UserObjectProvider.GetInstance;
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000009 RID: 9 RVA: 0x00002160 File Offset: 0x00000360
		[HelpKeyword("My.Forms")]
		internal static MyProject.MyForms Forms
		{
			[DebuggerHidden]
			get
			{
				return MyProject.m_MyFormsObjectProvider.GetInstance;
			}
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x0600000A RID: 10 RVA: 0x0000217C File Offset: 0x0000037C
		[HelpKeyword("My.WebServices")]
		internal static MyProject.MyWebServices WebServices
		{
			[DebuggerHidden]
			get
			{
				return MyProject.m_MyWebServicesObjectProvider.GetInstance;
			}
		}

		// Token: 0x04000001 RID: 1
		private static readonly MyProject.ThreadSafeObjectProvider<MyComputer> m_ComputerObjectProvider = new MyProject.ThreadSafeObjectProvider<MyComputer>();

		// Token: 0x04000002 RID: 2
		private static readonly MyProject.ThreadSafeObjectProvider<MyApplication> m_AppObjectProvider = new MyProject.ThreadSafeObjectProvider<MyApplication>();

		// Token: 0x04000003 RID: 3
		private static readonly MyProject.ThreadSafeObjectProvider<User> m_UserObjectProvider = new MyProject.ThreadSafeObjectProvider<User>();

		// Token: 0x04000004 RID: 4
		private static MyProject.ThreadSafeObjectProvider<MyProject.MyForms> m_MyFormsObjectProvider = new MyProject.ThreadSafeObjectProvider<MyProject.MyForms>();

		// Token: 0x04000005 RID: 5
		private static readonly MyProject.ThreadSafeObjectProvider<MyProject.MyWebServices> m_MyWebServicesObjectProvider = new MyProject.ThreadSafeObjectProvider<MyProject.MyWebServices>();

		// Token: 0x0200003E RID: 62
		[EditorBrowsable(EditorBrowsableState.Never)]
		[MyGroupCollection("System.Windows.Forms.Form", "Create__Instance__", "Dispose__Instance__", "My.MyProject.Forms")]
		internal sealed class MyForms
		{
			// Token: 0x060006B9 RID: 1721 RVA: 0x000339A4 File Offset: 0x00031BA4
			[DebuggerHidden]
			private static T Create__Instance__<T>(T Instance) where T : Form, new()
			{
				bool flag = Instance == null || Instance.IsDisposed;
				if (flag)
				{
					bool flag2 = MyProject.MyForms.m_FormBeingCreated != null;
					if (flag2)
					{
						bool flag3 = MyProject.MyForms.m_FormBeingCreated.ContainsKey(typeof(T));
						if (flag3)
						{
							throw new InvalidOperationException(Utils.GetResourceString("WinForms_RecursiveFormCreate", new string[0]));
						}
					}
					else
					{
						MyProject.MyForms.m_FormBeingCreated = new Hashtable();
					}
					MyProject.MyForms.m_FormBeingCreated.Add(typeof(T), null);
					try
					{
						return new T();
					}
					catch (TargetInvocationException ex) when (ex.InnerException != null)
					{
						string resourceString = Utils.GetResourceString("WinForms_SeeInnerException", new string[] { ex.InnerException.Message });
						throw new InvalidOperationException(resourceString, ex.InnerException);
					}
					finally
					{
						MyProject.MyForms.m_FormBeingCreated.Remove(typeof(T));
					}
				}
				return Instance;
			}

			// Token: 0x060006BA RID: 1722 RVA: 0x00033ACC File Offset: 0x00031CCC
			[DebuggerHidden]
			private void Dispose__Instance__<T>(ref T instance) where T : Form
			{
				instance.Dispose();
				instance = default(T);
			}

			// Token: 0x060006BB RID: 1723 RVA: 0x00033AE3 File Offset: 0x00031CE3
			[DebuggerHidden]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public MyForms()
			{
			}

			// Token: 0x060006BC RID: 1724 RVA: 0x00033AF0 File Offset: 0x00031CF0
			[EditorBrowsable(EditorBrowsableState.Never)]
			public override bool Equals(object o)
			{
				return base.Equals(RuntimeHelpers.GetObjectValue(o));
			}

			// Token: 0x060006BD RID: 1725 RVA: 0x00033B10 File Offset: 0x00031D10
			[EditorBrowsable(EditorBrowsableState.Never)]
			public override int GetHashCode()
			{
				return base.GetHashCode();
			}

			// Token: 0x060006BE RID: 1726 RVA: 0x00033B28 File Offset: 0x00031D28
			[EditorBrowsable(EditorBrowsableState.Never)]
			internal new Type GetType()
			{
				return typeof(MyProject.MyForms);
			}

			// Token: 0x060006BF RID: 1727 RVA: 0x00033B44 File Offset: 0x00031D44
			[EditorBrowsable(EditorBrowsableState.Never)]
			public override string ToString()
			{
				return base.ToString();
			}

			// Token: 0x170001F1 RID: 497
			// (get) Token: 0x060006C0 RID: 1728 RVA: 0x00033B5C File Offset: 0x00031D5C
			// (set) Token: 0x060006C4 RID: 1732 RVA: 0x00033BC8 File Offset: 0x00031DC8
			public AboutForm AboutForm
			{
				[DebuggerHidden]
				get
				{
					this.m_AboutForm = MyProject.MyForms.Create__Instance__<AboutForm>(this.m_AboutForm);
					return this.m_AboutForm;
				}
				[DebuggerHidden]
				set
				{
					if (value != this.m_AboutForm)
					{
						if (value != null)
						{
							throw new ArgumentException("Property can only be set to Nothing");
						}
						this.Dispose__Instance__<AboutForm>(ref this.m_AboutForm);
					}
				}
			}

			// Token: 0x170001F2 RID: 498
			// (get) Token: 0x060006C1 RID: 1729 RVA: 0x00033B77 File Offset: 0x00031D77
			// (set) Token: 0x060006C5 RID: 1733 RVA: 0x00033BF4 File Offset: 0x00031DF4
			public Form1 Form1
			{
				[DebuggerHidden]
				get
				{
					this.m_Form1 = MyProject.MyForms.Create__Instance__<Form1>(this.m_Form1);
					return this.m_Form1;
				}
				[DebuggerHidden]
				set
				{
					if (value != this.m_Form1)
					{
						if (value != null)
						{
							throw new ArgumentException("Property can only be set to Nothing");
						}
						this.Dispose__Instance__<Form1>(ref this.m_Form1);
					}
				}
			}

			// Token: 0x170001F3 RID: 499
			// (get) Token: 0x060006C2 RID: 1730 RVA: 0x00033B92 File Offset: 0x00031D92
			// (set) Token: 0x060006C6 RID: 1734 RVA: 0x00033C20 File Offset: 0x00031E20
			public frmDescription frmDescription
			{
				[DebuggerHidden]
				get
				{
					this.m_frmDescription = MyProject.MyForms.Create__Instance__<frmDescription>(this.m_frmDescription);
					return this.m_frmDescription;
				}
				[DebuggerHidden]
				set
				{
					if (value != this.m_frmDescription)
					{
						if (value != null)
						{
							throw new ArgumentException("Property can only be set to Nothing");
						}
						this.Dispose__Instance__<frmDescription>(ref this.m_frmDescription);
					}
				}
			}

			// Token: 0x170001F4 RID: 500
			// (get) Token: 0x060006C3 RID: 1731 RVA: 0x00033BAD File Offset: 0x00031DAD
			// (set) Token: 0x060006C7 RID: 1735 RVA: 0x00033C4C File Offset: 0x00031E4C
			public HelpForm HelpForm
			{
				[DebuggerHidden]
				get
				{
					this.m_HelpForm = MyProject.MyForms.Create__Instance__<HelpForm>(this.m_HelpForm);
					return this.m_HelpForm;
				}
				[DebuggerHidden]
				set
				{
					if (value != this.m_HelpForm)
					{
						if (value != null)
						{
							throw new ArgumentException("Property can only be set to Nothing");
						}
						this.Dispose__Instance__<HelpForm>(ref this.m_HelpForm);
					}
				}
			}

			// Token: 0x0400038A RID: 906
			[ThreadStatic]
			private static Hashtable m_FormBeingCreated;

			// Token: 0x0400038B RID: 907
			[EditorBrowsable(EditorBrowsableState.Never)]
			public AboutForm m_AboutForm;

			// Token: 0x0400038C RID: 908
			[EditorBrowsable(EditorBrowsableState.Never)]
			public Form1 m_Form1;

			// Token: 0x0400038D RID: 909
			[EditorBrowsable(EditorBrowsableState.Never)]
			public frmDescription m_frmDescription;

			// Token: 0x0400038E RID: 910
			[EditorBrowsable(EditorBrowsableState.Never)]
			public HelpForm m_HelpForm;
		}

		// Token: 0x0200003F RID: 63
		[EditorBrowsable(EditorBrowsableState.Never)]
		[MyGroupCollection("System.Web.Services.Protocols.SoapHttpClientProtocol", "Create__Instance__", "Dispose__Instance__", "")]
		internal sealed class MyWebServices
		{
			// Token: 0x060006C8 RID: 1736 RVA: 0x00033C78 File Offset: 0x00031E78
			[EditorBrowsable(EditorBrowsableState.Never)]
			[DebuggerHidden]
			public override bool Equals(object o)
			{
				return base.Equals(RuntimeHelpers.GetObjectValue(o));
			}

			// Token: 0x060006C9 RID: 1737 RVA: 0x00033C98 File Offset: 0x00031E98
			[EditorBrowsable(EditorBrowsableState.Never)]
			[DebuggerHidden]
			public override int GetHashCode()
			{
				return base.GetHashCode();
			}

			// Token: 0x060006CA RID: 1738 RVA: 0x00033CB0 File Offset: 0x00031EB0
			[EditorBrowsable(EditorBrowsableState.Never)]
			[DebuggerHidden]
			internal new Type GetType()
			{
				return typeof(MyProject.MyWebServices);
			}

			// Token: 0x060006CB RID: 1739 RVA: 0x00033CCC File Offset: 0x00031ECC
			[EditorBrowsable(EditorBrowsableState.Never)]
			[DebuggerHidden]
			public override string ToString()
			{
				return base.ToString();
			}

			// Token: 0x060006CC RID: 1740 RVA: 0x00033CE4 File Offset: 0x00031EE4
			[DebuggerHidden]
			private static T Create__Instance__<T>(T instance) where T : new()
			{
				bool flag = instance == null;
				T t;
				if (flag)
				{
					t = new T();
				}
				else
				{
					t = instance;
				}
				return t;
			}

			// Token: 0x060006CD RID: 1741 RVA: 0x00033D0D File Offset: 0x00031F0D
			[DebuggerHidden]
			private void Dispose__Instance__<T>(ref T instance)
			{
				instance = default(T);
			}

			// Token: 0x060006CE RID: 1742 RVA: 0x00033D17 File Offset: 0x00031F17
			[DebuggerHidden]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public MyWebServices()
			{
			}
		}

		// Token: 0x02000040 RID: 64
		[EditorBrowsable(EditorBrowsableState.Never)]
		[ComVisible(false)]
		internal sealed class ThreadSafeObjectProvider<T> where T : new()
		{
			// Token: 0x170001F5 RID: 501
			// (get) Token: 0x060006CF RID: 1743 RVA: 0x00033D24 File Offset: 0x00031F24
			internal T GetInstance
			{
				[DebuggerHidden]
				get
				{
					bool flag = MyProject.ThreadSafeObjectProvider<T>.m_ThreadStaticValue == null;
					if (flag)
					{
						MyProject.ThreadSafeObjectProvider<T>.m_ThreadStaticValue = new T();
					}
					return MyProject.ThreadSafeObjectProvider<T>.m_ThreadStaticValue;
				}
			}

			// Token: 0x060006D0 RID: 1744 RVA: 0x00033D56 File Offset: 0x00031F56
			[DebuggerHidden]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public ThreadSafeObjectProvider()
			{
			}

			// Token: 0x0400038F RID: 911
			[CompilerGenerated]
			[ThreadStatic]
			private static T m_ThreadStaticValue;
		}
	}
}
