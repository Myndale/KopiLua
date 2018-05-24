/*
** $Id: luac.c,v 1.54 2006/06/02 17:37:11 lhf Exp $
** Lua compiler (saves bytecodes to files; also list bytecodes)
** See Copyright Notice in lua.h
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static KopiLua.Lua;

namespace KopiLua
{
	using Instruction = System.UInt32;

	public class Program
	{
		static CharPtr PROGNAME = "luac";		/* default program name */
		static CharPtr OUTPUT = PROGNAME + ".out"; /* default output file */

		static int listing=0;			/* list bytecodes? */
		static int dumping=1;			/* dump bytecodes? */
		static int stripping=0;			/* strip debug information? */
		static CharPtr Output=OUTPUT;	/* default output file name */
		static CharPtr output=Output;	/* actual output file name */
		static CharPtr progname=PROGNAME;	/* actual program name */

		static void fatal(CharPtr message)
		{
		 fprintf(stderr,"%s: %s\n",progname,message);
		 Environment.Exit(EXIT_FAILURE);
		}

		static void cannot(CharPtr what)
		{
		 fprintf(stderr,"%s: cannot %s %s: %s\n",progname,what,output,strerror(errno()));
		 Environment.Exit(EXIT_FAILURE);
		}

		static void usage(CharPtr message)
		{
		 if (message[0]=='-')
		  fprintf(stderr,"%s: unrecognized option " + LUA_QS + "\n",progname,message);
		 else
		  fprintf(stderr,"%s: %s\n",progname,message);
		 fprintf(stderr,
		 "usage: %s [options] [filenames].\n" +
		 "Available options are:\n" +
		 "  -        process stdin\n" +
		 "  -l       list\n" +
		 "  -o name  output to file " + LUA_QL("name") + " (default is \"%s\")\n" +
		 "  -p       parse only\n" +
		 "  -s       strip debug information\n" +
		 "  -v       show version information\n" +
		 "  --       stop handling options\n",
		 progname,Output);
		 Environment.Exit(EXIT_FAILURE);
		}

		//#define	IS(s)	(strcmp(argv[i],s)==0)

		static int doargs(int argc, string[] argv)
		{
		 int i;
		 int version=0;
		 if ((argv.Length > 0) && (argv[0]!="")) progname=argv[0];
		 for (i=1; i<argc; i++)
		 {
		  if (argv[i][0]!='-')			/* end of options; keep it */
		   break;
		  else if (strcmp(argv[i], "--") == 0)			/* end of options; skip it */
		  {
		   ++i;
		   if (version!=0) ++version;
		   break;
		  }
		  else if (strcmp(argv[i], "-") == 0)			/* end of options; use stdin */
		   break;
		  else if (strcmp(argv[i], "-l") == 0)			/* list */
		   ++listing;
		  else if (strcmp(argv[i], "-o") == 0)			/* output file */
		  {
		   output=argv[++i];
		   if (output==null || (output[0]==0)) usage(LUA_QL("-o") + " needs argument");
		   if (strcmp(argv[i], "-")==0) output = null;
		  }
		  else if (strcmp(argv[i], "-p") == 0)			/* parse only */
		   dumping=0;
		  else if (strcmp(argv[i], "-s") == 0)			/* strip debug information */
		   stripping=1;
		  else if (strcmp(argv[i], "-v") == 0)			/* show version */
		   ++version;
		  else					/* unknown option */
		   usage(argv[i]);
		 }
		 if (i==argc && ((listing!=0) || (dumping==0)))
		 {
		  dumping=0;
		  argv[--i]=Output.ToString();
		 }
		 if (version!=0)
		 {
		  printf("%s  %s\n",LUA_RELEASE,LUA_COPYRIGHT);
		  if (version==argc-1) Environment.Exit(EXIT_SUCCESS);
		 }
		 return i;
		}

		static Proto toproto(lua_State L, int i) {return clvalue(L.top+(i)).l.p;}

		static Proto combine(lua_State L, int n)
		{
		 if (n==1)
		  return toproto(L,-1);
		 else
		 {
		  int i,pc;
		  Proto f=luaF_newproto(L);
		  setptvalue2s(L,L.top,f); incr_top(L);
		  f.source=luaS_newliteral(L,"=(" + PROGNAME + ")");
		  f.maxstacksize=1;
		  pc=2*n+1;
		  f.code = (Instruction[])luaM_newvector<Instruction>(L, pc);
		  f.sizecode=pc;
		  f.p = luaM_newvector<Proto>(L, n);
		  f.sizep=n;
		  pc=0;
		  for (i=0; i<n; i++)
		  {
		   f.p[i]=toproto(L,i-n-1);
		   f.code[pc++]=(uint)CREATE_ABx(OpCode.OP_CLOSURE,0,i);
		   f.code[pc++]=(uint)CREATE_ABC(OpCode.OP_CALL,0,1,1);
		  }
		  f.code[pc++]=(uint)CREATE_ABC(OpCode.OP_RETURN,0,1,0);
		  return f;
		 }
		}

		static int writer(lua_State L, CharPtr p, uint size, object u)
		{
		 //UNUSED(L);
		 return ((fwrite(p,(int)size,1,(Stream)u)!=1) && (size!=0)) ? 1 : 0;
		}

		public class Smain {
		 public int argc;
		 public string[] argv;
		};

		static int pmain(lua_State L)
		{
		 Smain s = (Smain)lua_touserdata(L, 1);
		 int argc=s.argc;
		 string[] argv=s.argv;
		 Proto f;
		 int i;
		 if (lua_checkstack(L,argc)==0) fatal("too many input files");
		 for (i=0; i<argc; i++)
		 {
		  CharPtr filename=(strcmp(argv[i], "-")==0) ? null : argv[i];
		  if (luaL_loadfile(L,filename)!=0) fatal(lua_tostring(L,-1));
		 }
		 f=combine(L,argc);
		 if (listing!=0) luaU_print(f,(listing>1)?1:0);
		 if (dumping!=0)
		 {
		  Stream D= (output==null) ? stdout : fopen(output,"wb");
		  if (D==null) cannot("open");
		  lua_lock(L);
		  luaU_dump(L,f,writer,D,stripping);
		  lua_unlock(L);
		  if (ferror(D)!=0) cannot("write");
		  if (fclose(D)!=0) cannot("close");
		 }
		 return 0;
		}

		static int Main(string[] args)
		{
		 // prepend the exe name to the arg list as it's done in C
		 // so that we don't have to change any of the args indexing
		 // code above
		 List<string> newargs = new List<string>(args);
		 newargs.Insert(0, Assembly.GetExecutingAssembly().Location);
		 args = (string[])newargs.ToArray();

		 lua_State L;
		 Smain s = new Smain();
		 int argc = args.Length;
		 int i=doargs(argc,args);
		 newargs.RemoveRange(0, i);
		 argc -= i; args = (string[])newargs.ToArray();
		 if (argc<=0) usage("no input files given");
		 L=lua_open();
		 if (L==null) fatal("not enough memory for state");
		 s.argc=argc;
		 s.argv=args;
		 if (lua_cpcall(L,pmain,s)!=0) fatal(lua_tostring(L,-1));
		 lua_close(L);
		 return EXIT_SUCCESS;
		}
		
	}
}
