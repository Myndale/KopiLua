/*
** $Id: lua.c,v 1.160.1.2 2007/12/28 15:32:23 roberto Exp $
** Lua stand-alone interpreter
** See Copyright Notice in lua.h
*/


using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using KopiLua;
using static KopiLua.Lua;

namespace KopiLua
{
	public class Program
	{
		static lua_State globalL = null;

		static CharPtr progname = LUA_PROGNAME;

		static void lstop(lua_State L, lua_Debug ar)
		{
			lua_sethook(L, null, 0, 0);
			luaL_error(L, "interrupted!");
		}


		static void laction(int i)
		{
			//signal(i, SIG_DFL); /* if another SIGINT happens before lstop,
			//						  terminate process (default action) */
			lua_sethook(globalL, lstop, LUA_MASKCALL | LUA_MASKRET | LUA_MASKCOUNT, 1);
		}


		static void print_usage()
		{
			Console.Error.Write(
			"usage: {0} [options] [script [args]].\n" +
			"Available options are:\n" +
			"  -e stat  execute string " + LUA_QL("stat").ToString() + "\n" +
			"  -l name  require library " + LUA_QL("name").ToString() + "\n" +
			"  -i       enter interactive mode after executing " + LUA_QL("script").ToString() + "\n" +
			"  -v       show version information\n" +
			"  --       stop handling options\n" +
			"  -        execute stdin and stop handling options\n"
			,
			progname);
			Console.Error.Flush();
		}


		static void l_message(CharPtr pname, CharPtr msg)
		{
			if (pname != null) fprintf(stderr, "%s: ", pname);
			fprintf(stderr, "%s\n", msg);
			fflush(stderr);
		}


		static int report(lua_State L, int status)
		{
			if ((status!=0) && !lua_isnil(L, -1))
			{
				CharPtr msg = lua_tostring(L, -1);
				if (msg == null) msg = "(error object is not a string)";
				l_message(progname, msg);
				lua_pop(L, 1);
			}
			return status;
		}


		static int traceback(lua_State L)
		{
			if (lua_isstring(L, 1)==0)  /* 'message' not a string? */
				return 1;  /* keep it intact */
			lua_getfield(L, LUA_GLOBALSINDEX, "debug");
			if (!lua_istable(L, -1))
			{
				lua_pop(L, 1);
				return 1;
			}
			lua_getfield(L, -1, "traceback");
			if (!lua_isfunction(L, -1))
			{
				lua_pop(L, 2);
				return 1;
			}
			lua_pushvalue(L, 1);  /* pass error message */
			lua_pushinteger(L, 2);  /* skip this function and traceback */
			lua_call(L, 2, 1);  /* call debug.traceback */
			return 1;
		}


		static int docall(lua_State L, int narg, int clear)
		{
			int status;
			int base_ = lua_gettop(L) - narg;  /* function index */
			lua_pushcfunction(L, traceback);  /* push traceback function */
			lua_insert(L, base_);  /* put it under chunk and args */
			//signal(SIGINT, laction);
			status = lua_pcall(L, narg, ((clear!=0) ? 0 : LUA_MULTRET), base_);
			//signal(SIGINT, SIG_DFL);
			lua_remove(L, base_);  /* remove traceback function */
			/* force a complete garbage collection in case of errors */
			if (status != 0) lua_gc(L, LUA_GCCOLLECT, 0);
			return status;
		}


		static void print_version()
		{
			l_message(null, LUA_RELEASE + "  " + LUA_COPYRIGHT);
		}


		static int getargs(lua_State L, string[] argv, int n)
		{
			int narg;
			int i;
			int argc = argv.Length;	/* count total number of arguments */
			narg = argc - (n + 1);  /* number of arguments to the script */
			luaL_checkstack(L, narg + 3, "too many arguments to script");
			for (i = n + 1; i < argc; i++)
			lua_pushstring(L, argv[i]);
			lua_createtable(L, narg, n + 1);
			for (i = 0; i < argc; i++)
			{
				lua_pushstring(L, argv[i]);
				lua_rawseti(L, -2, i - n);
			}
			return narg;
		}


		static int dofile(lua_State L, CharPtr name)
		{
			int status = (luaL_loadfile(L, name)!=0) || (docall(L, 0, 1)!=0) ? 1 : 0;
			return report(L, status);
		}


		static int dostring(lua_State L, CharPtr s, CharPtr name)
		{
			int status = (luaL_loadbuffer(L, s, (uint)strlen(s), name)!=0) || (docall(L, 0, 1)!=0) ? 1 : 0;
			return report(L, status);
		}


		static int dolibrary(lua_State L, CharPtr name)
		{
			lua_getglobal(L, "require");
			lua_pushstring(L, name);
			return report(L, docall(L, 1, 1));
		}


		static CharPtr get_prompt(lua_State L, int firstline)
		{
			CharPtr p;
			lua_getfield(L, LUA_GLOBALSINDEX, (firstline!=0) ? "_PROMPT" : "_PROMPT2");
			p = lua_tostring(L, -1);
			if (p == null) p = ((firstline!=0) ? LUA_PROMPT : LUA_PROMPT2);
			lua_pop(L, 1);  /* remove global */
			return p;
		}


		static int incomplete(lua_State L, int status)
		{
			if (status == LUA_ERRSYNTAX)
			{
				uint lmsg;
				CharPtr msg = lua_tolstring(L, -1, out lmsg);
				CharPtr tp = msg + lmsg - (strlen(LUA_QL("<eof>")));
				if (strstr(msg, LUA_QL("<eof>")) == tp)
				{
					lua_pop(L, 1);
					return 1;
				}
			}
			return 0;  /* else... */
		}


		static int pushline(lua_State L, int firstline)
		{
			CharPtr buffer = new char[LUA_MAXINPUT];
			CharPtr b = new CharPtr(buffer);
			int l;
			CharPtr prmt = get_prompt(L, firstline);
			if (!lua_readline(L, b, prmt))
				return 0;  /* no input */
			l = strlen(b);
			if (l > 0 && b[l - 1] == '\n')  /* line ends with newline? */
				b[l - 1] = '\0';  /* remove it */
			if ((firstline!=0) && (b[0] == '='))  /* first line starts with `=' ? */
				lua_pushfstring(L, "return %s", b + 1);  /* change it to `return' */
			else
				lua_pushstring(L, b);
			lua_freeline(L, b);
			return 1;
		}


		static int loadline(lua_State L)
		{
			int status;
			lua_settop(L, 0);
			if (pushline(L, 1)==0)
				return -1;  /* no input */
			for (; ; )
			{  /* repeat until gets a complete line */
				status = luaL_loadbuffer(L, lua_tostring(L, 1), lua_strlen(L, 1), "=stdin");
				if (incomplete(L, status)==0) break;  /* cannot try to add lines? */
				if (pushline(L, 0)==0)  /* no more input? */
					return -1;
				lua_pushliteral(L, "\n");  /* add a new line... */
				lua_insert(L, -2);  /* ...between the two lines */
				lua_concat(L, 3);  /* join them */
			}
			lua_saveline(L, 1);
			lua_remove(L, 1);  /* remove line */
			return status;
		}


		static void dotty(lua_State L)
		{
			int status;
			CharPtr oldprogname = progname;
			progname = null;
			while ((status = loadline(L)) != -1)
			{
				if (status == 0) status = docall(L, 0, 0);
				report(L, status);
				if (status == 0 && lua_gettop(L) > 0)
				{  /* any result to print? */
					lua_getglobal(L, "print");
					lua_insert(L, 1);
					if (lua_pcall(L, lua_gettop(L) - 1, 0, 0) != 0)
						l_message(progname, lua_pushfstring(L,
											   "error calling " + LUA_QL("print").ToString() + " (%s)",
											   lua_tostring(L, -1)));
				}
			}
			lua_settop(L, 0);  /* clear stack */
			fputs("\n", stdout);
			fflush(stdout);
			progname = oldprogname;
		}


		static int handle_script(lua_State L, string[] argv, int n)
		{
			int status;
			CharPtr fname;
			int narg = getargs(L, argv, n);  /* collect arguments */
			lua_setglobal(L, "arg");
			fname = argv[n];
			if (strcmp(fname, "-") == 0 && strcmp(argv[n - 1], "--") != 0)
				fname = null;  /* stdin */
			status = luaL_loadfile(L, fname);
			lua_insert(L, -(narg + 1));
			if (status == 0)
				status = docall(L, narg, 0);
			else
				lua_pop(L, narg);
			return report(L, status);
		}


		/* check that argument has no extra characters at the end */
		//#define notail(x)	{if ((x)[2] != '\0') return -1;}


		static int collectargs(string[] argv, ref int pi, ref int pv, ref int pe)
		{
			int i;
			for (i = 1; i < argv.Length; i++)
			{
				if (argv[i][0] != '-')  /* not an option? */
					return i;
				switch (argv[i][1])
				{  /* option */
					case '-':
						if (argv[i].Length != 2) return -1;
						return (i + 1) >= argv.Length ? i + 1 : 0;

					case '\0':
						return i;

					case 'i':
						if (argv[i].Length != 2) return -1;
						pi = 1;
						if (argv[i].Length != 2) return -1;
						pv = 1;
						break;

					case 'v':
						if (argv[i].Length != 2) return -1;
						pv = 1;
						break;

					case 'e':
						pe = 1;
						if (argv[i].Length == 2)
						{
							i++;
							if (argv[i] == null) return -1;
						}
						break;

					case 'l':
						if (argv[i].Length == 2)
						{
							i++;
							if (i >= argv.Length) return -1;
						}
						break;
					default: return -1;  /* invalid option */
				}
			}
			return 0;
		}


		static int runargs(lua_State L, string[] argv, int n)
		{
			int i;
			for (i = 1; i < n; i++)
			{
				if (argv[i] == null) continue;
				lua_assert(argv[i][0] == '-');
				switch (argv[i][1])
				{  /* option */
					case 'e':
						{
							string chunk = argv[i].Substring(2);
							if (chunk == "") chunk = argv[++i];
							lua_assert(chunk != null);
							if (dostring(L, chunk, "=(command line)") != 0)
								return 1;
							break;
						}
					case 'l':
						{
							string filename = argv[i].Substring(2);
							if (filename == "") filename = argv[++i];
							lua_assert(filename != null);
							if (dolibrary(L, filename) != 0)
								return 1;  /* stop if file fails */
							break;
						}
					default: break;
				}
			}
			return 0;
		}


		static int handle_luainit(lua_State L)
		{
			CharPtr init = getenv(LUA_INIT);
			if (init == null) return 0;  /* status OK */
			else if (init[0] == '@')
				return dofile(L, init + 1);
			else
				return dostring(L, init, "=" + LUA_INIT);
		}


		public class Smain
		{
			public int argc;
			public string[] argv;
			public int status;
		};


		static int pmain(lua_State L)
		{
			Smain s = (Smain)lua_touserdata(L, 1);
			string[] argv = s.argv;
			int script;
			int has_i = 0, has_v = 0, has_e = 0;
			globalL = L;
			if ((argv.Length>0) && (argv[0]!="")) progname = argv[0];
			lua_gc(L, LUA_GCSTOP, 0);  /* stop collector during initialization */
			luaL_openlibs(L);  /* open libraries */
			lua_gc(L, LUA_GCRESTART, 0);
			s.status = handle_luainit(L);
			if (s.status != 0) return 0;
			script = collectargs(argv, ref has_i, ref has_v, ref has_e);
			if (script < 0)
			{  /* invalid args? */
				print_usage();
				s.status = 1;
				return 0;
			}
			if (has_v!=0) print_version();
			s.status = runargs(L, argv, (script > 0) ? script : s.argc);
			if (s.status != 0) return 0;
			if (script!=0)
				s.status = handle_script(L, argv, script);
			if (s.status != 0) return 0;
			if (has_i!=0)
				dotty(L);
			else if ((script==0) && (has_e==0) && (has_v==0))
			{
				if (lua_stdin_is_tty()!=0)
				{
					print_version();
					dotty(L);
				}
				else dofile(L, null);  /* executes stdin as a file */
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

			int status;
			Smain s = new Smain();
			lua_State L = lua_open();  /* create state */
			if (L == null)
			{
				l_message(args[0], "cannot create state: not enough memory");
				return EXIT_FAILURE;
			}
			s.argc = args.Length;
			s.argv = args;
			status = lua_cpcall(L, pmain, s);
			report(L, status);
			lua_close(L);
			return (status!=0) || (s.status!=0) ? EXIT_FAILURE : EXIT_SUCCESS;
		}

	}
}
