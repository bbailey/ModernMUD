using System;
using System.Collections.Generic;
using System.IO;
using HelpData;
using ModernMUD;

namespace MUDEngine
{
    public class Database
    {
        public static List<Help> HelpList = new List<Help>();
        // Because we need to keep track of the last area.
        public static readonly LinkedList<Area> AreaList = new LinkedList<Area>();
        public static readonly List<Vehicle> VehicleList = new List<Vehicle>();
        public static readonly List<BanData> BanList = new List<BanData>();
        public static readonly List<Event> EventList = new List<Event>();
        public List<ZoneConnection> ZoneConnectionList = new List<ZoneConnection>();
        public static List<Crime> CrimeList = new List<Crime>();
        public static List<Message> NoteList = new List<Message>();
        public static readonly List<CastData> CastList = new List<CastData>(); // To keep track of everyone casting spells
        public static readonly List<Guild> GuildList = new List<Guild>();
        public static List<Issue> IssueList = new List<Issue>();
        public static readonly List<CharData> CharList = new List<CharData>();
        public static readonly List<Object> ObjectList = new List<Object>();
        public static readonly List<SocketConnection> SocketList = new List<SocketConnection>();
        public static List<ChatterBot> ChatterBotList = new List<ChatterBot>();
        public static readonly Dictionary<String, Song> SongList = new Dictionary<String, Song>();
        public static readonly Dictionary<String, MonkSkill> MonkSkillList = new Dictionary<String, MonkSkill>();
        public static Socials SocialList;
        public static CorpseData CorpseList;
        public static Screen ScreenList;
        public static bool Reboot;
        public static Sysdata SystemData;

        public static int HighestRoomIndexNumber { get; set; }
        public static int HighestMobIndexNumber { get; set; }
        public static int HighestObjIndexNumber { get; set; }
        public static bool DatabaseIsBooting { get; set; }

        /// <summary>
        /// Loads the entire MUD database including all classes, races, areas, helps, sytem data, etc.
        /// </summary>
        public void LoadDatabase()
        {
            DatabaseIsBooting = true;

            Log.Trace( DateTime.Now.ToShortDateString() + " BOOT: -------------------------[ Boot Log ]-------------------------" );

            Log.Trace("Validating that player directories exist.");
            for (Char letter = 'a'; letter <= 'z'; letter++)
            {
                String directory = FileLocation.PlayerDirectory + letter;
                if (!Directory.Exists(directory))
                {
                    Log.Trace("Creating directory: " + directory + ".");
                    Directory.CreateDirectory(directory);
                }
            }
            Log.Trace("Player directories validated.");

            Log.Trace( "Loading Database.SystemData." );
            Sysdata.Load();
            SystemData.CurrentTime = DateTime.Now;
            SystemData.GameBootTime = SystemData.CurrentTime;

            // Set time and weather.
            Log.Trace( "Setting time and weather." );
            SystemData.SetWeather();

            Log.Trace("Loading static rooms.");
            StaticRooms.Load();
            Log.Trace("Loaded " + StaticRooms.Count + " static rooms.");

            Log.Trace("Loading spells.");
            Spell.LoadSpells();
            Log.Trace("Loaded " + Spell.SpellList.Count + " spells.");

            Log.Trace("Loading skills.");
            Skill.LoadSkills();
            Log.Trace("Loaded " + Skill.SkillList.Count + " skills.");

            Log.Trace( "Loading races." );
            Race.LoadRaces();
            Log.Trace( "Loaded " + Race.Count + " races." );

            Log.Trace( "Initializing skill Levels." );
            {
                int cclass;

                foreach (KeyValuePair<String, Skill> kvp in Skill.SkillList)
                {
                    for (cclass = 0; cclass < CharClass.ClassList.Length; cclass++)
                        kvp.Value.ClassAvailability[ cclass ] = Limits.LEVEL_LESSER_GOD;
                }
                foreach (KeyValuePair<String, Spell> kvp in Spell.SpellList)
                {
                    for (cclass = 0; cclass < CharClass.ClassList.Length; cclass++)
                        kvp.Value.SpellCircle[cclass] = Limits.MAX_CIRCLE + 3;
                }
            }

            Log.Trace( "Loading classes." );
            CharClass.LoadClasses(true);
            Log.Trace( "Loaded " + CharClass.Count + " classes." );

            Log.Trace("Assigning spell circles.");
            AssignSpellCircles();
            Log.Trace("Assigned spell circles.");

            Log.Trace( "Loading socials." );
            SocialList = Socials.Load();
            Log.Trace( "Loaded " + Social.Count + " socials." );

            Log.Trace( "Loading bans." );
            LoadBans();
            Log.Trace( "Loaded " + BanList.Count + " bans." );

            Log.Trace( "Loading help entries." );
            HelpList = Help.Load(FileLocation.SystemDirectory + FileLocation.HelpFile);
            Log.Trace( "Loaded " + Help.Count + " help entries." );

            Log.Trace( "Loading screens." );
            Screen.Load(FileLocation.SystemDirectory + FileLocation.ScreenFile, FileLocation.BlankSystemFileDirectory + FileLocation.ScreenFile);
            Log.Trace( "Loaded " + Screen.Count + " screens." );

            // Chatbots have to be loaded before mobs.
            Log.Trace( "Loading chatbots." );
            ChatterBot.Load();
            Log.Trace( "Loaded " + ChatterBot.Count + " chatbots." );

            // Read in all the area files.
            Log.Trace( "Reading in area files..." );
            LoadAreaFiles();
            Log.Trace( "Loaded " + Area.Count + " areas." );

            string buf = String.Format( "Loaded {0} mobs, {1} objects, {2} rooms, {3} shops, {4} helps, {5} resets, and {6} quests.",
                                        MobTemplate.Count, ObjTemplate.Count, Room.Count, Shop.Count, Help.Count, Reset.Count, QuestData.Count );
            Log.Trace( buf );

            Log.Trace( "Loading guilds." );
            Guild.LoadGuilds();
            Log.Trace( "Loaded " + Guild.Count + " guilds." );

            Log.Trace( "Loading corpses." );
            CorpseList = CorpseData.Load();
            Log.Trace( "Loaded " + CorpseData.Count + " corpses." );

            Log.Trace( "Loading crimes." );
            Crime.Load();
            Log.Trace( "Loaded " + Crime.Count + " crimes." );

            Log.Trace( "Loading fraglist." );
            FraglistData.Fraglist.Load();

            Log.Trace( "Loading issues." );
            Issue.Load();
            Log.Trace( "Loaded " + Issue.Count + " issues." );

            Log.Trace("Loading bounties.");
            Bounty.Load();
            Log.Trace("Loaded " + Bounty.Count + " bounties.");

            Log.Trace("Initializing movement parameters.");
            Movement.Initialize();
            Log.Trace("Movement parameters initialized.");

            // Only compile spells that have attached code.  Otherwise use default handlers.
            Log.Trace("Compiling spells.");
            int good = 0;
            int bad = 0;
            foreach (KeyValuePair<String, Spell> kvp in Spell.SpellList)
            {
                if( !String.IsNullOrEmpty(kvp.Value.Code))
                {
                    if (!SpellFunction.CompileSpell(kvp.Value))
                    {
                        ++bad;
                    }
                    else
                    {
                        ++good;
                    }
                }
            }
            Log.Trace("Done compiling spells. " + good + " were successful, " + bad + " failed.");
            
            // Links up exits and makes rooms runtime-ready so we can access them.
            Log.Trace( "Linking exits." );
            LinkExits();

            // This has to be after LinkExits().
            Log.Trace("Loading zone connections.");
            ZoneConnectionList = ZoneConnection.Load();
            // Link zones together based on file.
            foreach (ZoneConnection connection in ZoneConnectionList)
            {
                RoomTemplate room1 = Room.GetRoom(connection.FirstRoomNumber);
                RoomTemplate room2 = Room.GetRoom(connection.SecondRoomNumber);
                Exit.Direction direction = connection.FirstToSecondDirection;
                if (room1 != null && room2 != null && direction != Exit.Direction.invalid)
                {
                    Exit exit = new Exit();
                    exit.TargetRoom = room2;
                    exit.IndexNumber = connection.SecondRoomNumber;
                    room1.ExitData[(int)direction] = exit;
                    exit = new Exit();
                    exit.TargetRoom = room1;
                    exit.IndexNumber = connection.FirstRoomNumber;
                    room2.ExitData[(int)Exit.ReverseDirection(direction)] = exit;
                    Log.Trace("Connected " + room1.Area.Name + " to " + room2.Area.Name + " at " + room1.IndexNumber);
                }
                else
                {
                    Log.Error("Unable to connect room " + connection.FirstRoomNumber + " to " + connection.SecondRoomNumber + " in direction " + connection.FirstToSecondDirection);
                }
            }
            Log.Trace("Loaded " + ZoneConnectionList.Count + " zone connections.");
            
            
            DatabaseIsBooting = false;
            Log.Trace( "Resetting areas." );
            AreaUpdate();

            Log.Trace( "Creating events." );
            Event.CreateEvent(Event.EventType.save_corpses, Event.TICK_SAVE_CORPSES, null, null, null);
            Event.CreateEvent(Event.EventType.save_sysdata, Event.TICK_SAVE_SYSDATA, null, null, null);
            Event.CreateEvent(Event.EventType.violence_update, Event.TICK_COMBAT_UPDATE, null, null, null);
            Event.CreateEvent(Event.EventType.area_update, Event.TICK_AREA, null, null, null);
            Event.CreateEvent(Event.EventType.room_update, Event.TICK_ROOM, null, null, null);
            Event.CreateEvent(Event.EventType.object_special, Event.TICK_OBJECT, null, null, null);
            Event.CreateEvent(Event.EventType.mobile_update, Event.TICK_MOBILE, null, null, null);
            Event.CreateEvent(Event.EventType.weather_update, Event.TICK_WEATHER, null, null, null);
            Event.CreateEvent(Event.EventType.char_update, Event.TICK_CHAR_UPDATE, null, null, null);
            Event.CreateEvent(Event.EventType.object_update, Event.TICK_OBJ_UPDATE, null, null, null);
            Event.CreateEvent(Event.EventType.aggression_update, Event.TICK_AGGRESS, null, null, null);
            Event.CreateEvent( Event.EventType.memorize_update, Event.TICK_MEMORIZE, null, null, null );
            Event.CreateEvent( Event.EventType.hit_gain, Event.TICK_HITGAIN, null, null, null );
            Event.CreateEvent( Event.EventType.mana_gain, Event.TICK_MANAGAIN, null, null, null );
            Event.CreateEvent( Event.EventType.move_gain, Event.TICK_MOVEGAIN, null, null, null );
            Event.CreateEvent( Event.EventType.heartbeat, Event.TICK_WEATHER, null, null, null );

            return;
        }

        /// <summary>
        /// Load all of the area files.
        /// </summary>
        public static void LoadAreaFiles()
        {
            if( !DatabaseIsBooting )
            {
                Log.Error( "LoadAreaFiles: Can't load area files if not booting!", 0 );
                return;
            }

            string filename = FileLocation.AreaDirectory + FileLocation.AreaLoadList;
            string emptyFilename = FileLocation.BlankAreaFileDirectory + FileLocation.AreaLoadList;
            FileStream fp = null;
            try
            {
                try
                {
                    fp = File.OpenRead(filename);
                }
                catch (FileNotFoundException)
                {
                    File.Copy(emptyFilename, filename);
                    fp = File.OpenRead(emptyFilename);
                }
                StreamReader sr = new StreamReader( fp );

                while( !sr.EndOfStream )
                {
                    string line = sr.ReadLine();

                    if( line[ 0 ] == '$' )
                    {
                        break;
                    }

                    Log.Trace(String.Format("Loading area: {0}", line));
                    String areaFilename = FileLocation.AreaDirectory + line;
                    Area area = Area.Load(areaFilename);

                    if (area == null)
                    {
                        Log.Error("Could not find area file " + line + ". Trying to copy from EmptyFiles directory.");
                        String backupFilename = FileLocation.BlankAreaFileDirectory + line;
                        try
                        {
                            File.Copy(backupFilename, areaFilename);
                            area = Area.Load(areaFilename);
                        }
                        catch(Exception)
                        {
                        }
                    }

                    if( area != null )
                    {
                        // Check for area index number overlap
                        foreach (Area previousArea in AreaList)
                        {
                            if ((area.LowIndexNumber > previousArea.LowIndexNumber && area.LowIndexNumber < previousArea.HighIndexNumber) ||
                                (area.HighIndexNumber < previousArea.HighIndexNumber && area.HighIndexNumber > previousArea.LowIndexNumber) ||
                                (area.LowIndexNumber < previousArea.LowIndexNumber && area.HighIndexNumber > previousArea.HighIndexNumber))
                            {
                                Log.Error(String.Format( "Area {0} has index numbers that range from {1} to {2}.  This overlaps the indexes of area {3} that range from {4} to {5}.  This could cause problems.",
                                    line, area.LowIndexNumber, area.HighIndexNumber, SocketConnection.RemoveANSICodes(previousArea.Name), previousArea.LowIndexNumber, previousArea.HighIndexNumber));
                            }
                        }
                        AreaList.AddLast(area);
                        Log.Trace( "Area loaded with {0} mobs, {1} objects, {2} rooms, {3} shops, {4} resets, and {5} quests.",
                            area.Mobs.Count, area.Objects.Count, area.Rooms.Count, 
                            area.Shops.Count, area.Resets.Count, area.Quests.Count );
                    }
                    else
                    {
                        Log.Error("Area file " + line + " failed to load.");
                    }
                }
            }
            catch( Exception ex )
            {
                Log.Error( "Exception in Database.LoadAreaFiles(): " + ex );
            }
        }

        /// <summary>
        /// Translates a dragon breath type _name into its special function.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static MobSpecial GetBreathType( string name )
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if( MUDString.NameContainedIn( "br_f", name ) )
                return MobSpecial.SpecMobLookup( "spec_breath_fire" )[0];
            if( MUDString.NameContainedIn( "br_a", name ) )
                return MobSpecial.SpecMobLookup("spec_breath_acid")[0];
            if( MUDString.NameContainedIn( "br_c", name ) )
                return MobSpecial.SpecMobLookup("spec_breath_frost")[0];
            if( MUDString.NameContainedIn( "br_g", name ) )
                return MobSpecial.SpecMobLookup("spec_breath_gas")[0];
            if( MUDString.NameContainedIn( "br_l", name ) )
                return MobSpecial.SpecMobLookup("spec_breath_lightning")[0];
            if( MUDString.NameContainedIn( "br_w", name ) )
                return MobSpecial.SpecMobLookup("spec_breath_water")[0];
            if( MUDString.NameContainedIn( "br_s", name ) )
                return MobSpecial.SpecMobLookup("spec_breath_shadow")[0];

            return MobSpecial.SpecMobLookup("spec_breath_any")[0];
        }

        /// <summary>
        /// Loads the ban file from disk.
        /// </summary>
        public static void LoadBans()
        {
            BanData ban;
            string fileLocation = FileLocation.SystemDirectory + FileLocation.BanFile;
            string blankFileLocation = FileLocation.BlankSystemFileDirectory + FileLocation.BanFile;

            try
            {
                FileStream fp = null;
                try
                {
                    fp = File.OpenRead(fileLocation);
                }
                catch (FileNotFoundException)
                {
                    Log.Info("Ban file not found, using blank file.");
                    File.Copy(blankFileLocation, fileLocation);
                    fp = File.OpenRead(fileLocation);
                }
                StreamReader sr = new StreamReader(fp);
                while (!sr.EndOfStream)
                {
                    string name = sr.ReadLine();

                    if (name[0] == '$')
                        break;

                    ban = new BanData();
                    ban.Name = name;
                    BanList.Add(ban);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception in Database.LoadBans(): " + ex);
            }
        }

        /// <summary>
        /// Assigns spell circles 
        /// </summary>
        private void AssignSpellCircles()
        {
            if (CharClass.ClassList == null)
            {
                Log.Error("Database.AssignSpellCircles():  Class list is empty -- no classes have been loaded.  Cannot assign spell circles.");
                return;
            }

            // Reset levels to what matches classes.
            foreach (CharClass charclass in CharClass.ClassList)
            {
                if (charclass.Spells == null)
                {
                    continue;
                }
                foreach (SpellEntry spellentry in charclass.Spells)
                {
                    if (Spell.SpellList.ContainsKey(spellentry.Name))
                    {
                        Spell.SpellList[spellentry.Name].SpellCircle[(int)charclass.ClassNumber] = spellentry.Circle;
                    }
                }
            }
        }

        /// <summary>
        /// Links all room exits to the the appropriate _targetType room data.  Reports any problems with
        /// nonexistant rooms or one-way exits.  This must be done once and only once after all rooms are
        /// loaded.
        /// </summary>
        static void LinkExits()
        {
            Exit exit;
            Exit reverseExit;
            Room toRoom;
            int door;

            // First we have to convert into runtime rooms.
            foreach (Area area in AreaList)
            {
                for (int i = 0; i < area.Rooms.Count; i++)
                {
                    area.Rooms[i] = new Room(area.Rooms[i]);
                }
            }

            foreach( Area area in AreaList )
            {
                foreach( Room room in area.Rooms )
                {
                    // Set exit data.
                    for (door = 0; door < Limits.MAX_DIRECTION; door++)
                    {
                        exit = room.ExitData[door];
                        if (exit != null)
                        {
                            if (exit.IndexNumber <= 0)
                            {
                                exit.TargetRoom = null;
                            }
                            else
                            {
                                exit.TargetRoom = Room.GetRoom(exit.IndexNumber);
                                if (exit.TargetRoom == null)
                                {
                                    string buf = String.Format("Room {0} in zone {1} has an exit in direction {2} to room {3}.  Room {3} was not found.",
                                                 room.IndexNumber, SocketConnection.RemoveANSICodes(room.Area.Name),
                                                 door.ToString(), exit.IndexNumber);
                                    Log.Error(buf);
                                    // NOTE: We do not delete the exit data here because most non-linkable exits are due to 
                                    // attached zones not loading.  If we delete the exit data, that means that the zone link
                                    // will be irrevocably lost if we re-save the zone.  However, if we leave the exit data
                                    // intact, when the missing zone is re-loaded in a future boot the exit should self-heal.
                                }
                            }
                        }
                    }

                }
            }

            foreach( Area area in AreaList )
            {
                foreach( RoomTemplate room in area.Rooms )
                {
                    for( door = 0; door < Limits.MAX_DIRECTION; door++ )
                    {
                        if( ( exit = room.ExitData[ door ] ) && ( toRoom = Room.GetRoom(exit.IndexNumber) )
                            && ( reverseExit = toRoom.GetExit(Exit.ReverseDirection((Exit.Direction)door)) )
                            && reverseExit.TargetRoom != room )
                        {
                            String buf = String.Format("Database.LinkExits(): Mismatched Exit - Room {0} Exit {1} to Room {2} in zone {3}: Target room's {4} Exit points to Room {5}.",
                                         room.IndexNumber, door.ToString(), toRoom.IndexNumber, 
                                         SocketConnection.RemoveANSICodes(room.Area.Name), Exit.ReverseDirection((Exit.Direction)door).ToString(),
                                         (!reverseExit.TargetRoom) ? 0 : reverseExit.TargetRoom.IndexNumber );
                            Log.Info( buf );
                        }
                    }
                }
            }
            return;
        }

        /// <summary>
        /// Repopulate areas periodically.
        /// 
        /// Also gradually replenishes a town's guard store if guards have been killed.
        /// </summary>
        static public void AreaUpdate()
        {
            int numPlayers = 0;
            foreach( Area area in AreaList )
            {
                CharData roomChar;

                if (area.NumDefendersDispatched > 0)
                {
                    area.NumDefendersDispatched--;
                }

                // Increment the area's age in seconds.
                area.AgeInSeconds += Event.TICK_AREA;

                // Reset area normally.
                if( area.AreaResetMode == Area.ResetMode.normal && area.TimesReset != 0 )
                    continue;
                // Reset area only when empty of players
                if (area.AreaResetMode == Area.ResetMode.empty_of_players && area.NumPlayers > 0)
                {
                    String text = String.Format("{0} not being Reset, {1} players are present.", area.Filename, area.NumPlayers);
                    ImmortalChat.SendImmortalChat(null, ImmortalChat.IMMTALK_RESETS, Limits.LEVEL_OVERLORD, text);
                    Log.Trace(text);
                    continue;
                }
                // Reset area only when no objects are still in the area.
                if (area.AreaResetMode == Area.ResetMode.empty_of_objects)
                {
                    foreach (Room room in area.Rooms)
                    {
                        foreach( Object obj in room.Contents )
                        {
                            if (obj.HasWearFlag(ObjTemplate.WEARABLE_CARRY))
                            {
                                String buf = String.Format("{0} not being Reset, at least one takeable object is present.", area.Filename);
                                ImmortalChat.SendImmortalChat(null, ImmortalChat.IMMTALK_RESETS, Limits.LEVEL_OVERLORD, buf);
                                Log.Trace(buf);
                                continue;
                            }
                        }
                    }
                }
                // Reset area only when no mobiles are alive in the area.
                if (area.AreaResetMode == Area.ResetMode.empty_of_mobiles)
                {
                    foreach (Room room in area.Rooms)
                    {
                        foreach (CharData ch in room.People)
                        {
                            if (ch.IsNPC())
                            {
                                String buf = String.Format("{0} not being Reset, at least one mobile is present.", area.Filename);
                                ImmortalChat.SendImmortalChat(null, ImmortalChat.IMMTALK_RESETS, Limits.LEVEL_OVERLORD, buf);
                                Log.Trace(buf);
                                continue;
                            }
                        }
                    }
                }
                // Reset area only when all quests are completed.
                if (area.AreaResetMode == Area.ResetMode.all_quests_completed)
                {
                    foreach (QuestTemplate qst in area.Quests)
                    {
                        foreach (QuestData quest in qst.Quests)
                        {
                            foreach (QuestItem item in quest.Receive)
                            {
                                if (item.Completed == false)
                                {
                                    String buf = String.Format("{0} not being Reset, at least one quest has not been completed.", area.Filename);
                                    ImmortalChat.SendImmortalChat(null, ImmortalChat.IMMTALK_RESETS, Limits.LEVEL_OVERLORD, buf);
                                    Log.Trace(buf);
                                    continue;
                                }
                            }
                        }
                    }
                }
                if ((area.AgeInSeconds / 60) < area.MinutesBetweenResets && area.TimesReset != 0)
                    continue;

                foreach( CharData cd in CharList )
                {
                    roomChar = cd;
                    if( !roomChar.IsNPC() && roomChar.InRoom != null && roomChar.InRoom.Area == area )
                        numPlayers++;
                }

                // Check for PC's and notify them if necessary.
                if( area.NumPlayers > 0 )
                {
                    foreach( CharData chd in CharList )
                    {
                        roomChar = chd;
                        if( !roomChar.IsNPC() && roomChar.IsAwake() && roomChar.InRoom
                                && roomChar.InRoom.Area == area &&
                                !String.IsNullOrEmpty(area.ResetMessage))
                        {
                            roomChar.SendText( String.Format( "{0}\r\n", area.ResetMessage ) );
                        }
                    }
                }

                // Check age and Reset.
                String outbuf = String.Format("{0} has just been Reset after {1} minutes.", area.Filename, (area.AgeInSeconds / 60));
                ImmortalChat.SendImmortalChat( null, ImmortalChat.IMMTALK_RESETS, Limits.LEVEL_OVERLORD, outbuf );
                ResetArea( area );
            }

            return;
        }

        /// <summary>
        /// Resets a single area.
        /// </summary>
        public static void ResetArea( Area area )
        {
            if (area == null)
            {
                return;
            }
            Room room;
            int indexNumber;
            area.DefenderSquads = 0;
            area.NumDefendersDispatched = 0;
            for( indexNumber = area.LowRoomIndexNumber; indexNumber <= area.HighRoomIndexNumber; indexNumber++ )
            {
                if (indexNumber == 0)
                    continue;
                room = Room.GetRoom( indexNumber );
                if( room && room.Area == area )
                {
                    room.ResetRoom( area.TimesReset );
                }
            }
            area.AgeInSeconds = MUDMath.NumberRange(-Event.AREA_RESET_VARIABILITY, Event.AREA_RESET_VARIABILITY);
            area.TimesReset++;
            return;
        }

        /// <summary>
        /// Create an instance of a mobile from the provided template.
        /// </summary>
        /// <param name="mobTemplate"></param>
        /// <returns></returns>
        public static CharData CreateMobile( MobTemplate mobTemplate )
        {
            int count;

            if( !mobTemplate )
            {
                Log.Error("CreateMobile: null MobTemplate.", 0);
                throw new NullReferenceException();
            }

            CharData mob = new CharData();

            mob.MobileTemplate = mobTemplate;
            mob.Followers = null;
            mob.Name = mobTemplate.PlayerName;
            mob.ShortDescription = mobTemplate.ShortDescription;
            mob.FullDescription = mobTemplate.FullDescription;
            mob.Description = mobTemplate.Description;
            mob.SpecialFunction = mobTemplate.SpecFun;
            mob.SpecialFunctionNames = mobTemplate.SpecFunNames;
            mob.CharacterClass = mobTemplate.CharacterClass;
            mob.Level = MUDMath.FuzzyNumber( mobTemplate.Level );
            mob.ActionFlags = mobTemplate.ActionFlags;
            mob.CurrentPosition = mobTemplate.DefaultPosition;
            mob.ChatterBotName = mobTemplate.ChatterBotName;
            // TODO: Look up the chatter bot name and load a runtime bot into the variable.
            mob.ChatBot = null;
            for( count = 0; count < Limits.NUM_AFFECT_VECTORS; ++count )
            {
                mob.AffectedBy[ count ] = mobTemplate.AffectedBy[ count ];
            }
            mob.Alignment = mobTemplate.Alignment;
            mob.Gender = mobTemplate.Gender;
            mob.SetPermRace( mobTemplate.Race );
            mob.CurrentSize = Race.RaceList[ mob.GetRace() ].DefaultSize;
            if (mob.HasActionBit(MobTemplate.ACT_SIZEMINUS))
                mob.CurrentSize--;
            if (mob.HasActionBit(MobTemplate.ACT_SIZEPLUS))
                mob.CurrentSize++;

            mob.CastingSpell = 0;
            mob.CastingTime = 0;
            mob.PermStrength = MUDMath.Dice( 2, 46 ) + 8;
            mob.PermIntelligence = MUDMath.Dice( 2, 46 ) + 8;
            mob.PermWisdom = MUDMath.Dice( 2, 46 ) + 8;
            mob.PermDexterity = MUDMath.Dice( 2, 46 ) + 8;
            mob.PermConstitution = MUDMath.Dice( 2, 46 ) + 7;
            mob.PermAgility = MUDMath.Dice( 2, 46 ) + 8;
            mob.PermCharisma = MUDMath.Dice( 2, 46 ) + 8;
            mob.PermPower = MUDMath.Dice( 2, 46 ) + 8;
            mob.PermLuck = MUDMath.Dice( 2, 46 ) + 8;
            mob.ModifiedStrength = 0;
            mob.ModifiedIntelligence = 0;
            mob.ModifiedWisdom = 0;
            mob.ModifiedDexterity = 0;
            mob.ModifiedConstitution = 0;
            mob.ModifiedAgility = 0;
            mob.ModifiedCharisma = 0;
            mob.ModifiedPower = 0;
            mob.ModifiedLuck = 0;
            mob.Resistant = mobTemplate.Resistant;
            mob.Immune = mobTemplate.Immune;
            mob.Susceptible = mobTemplate.Susceptible;
            mob.Vulnerable = mobTemplate.Vulnerable;
            mob.MaxMana = mob.Level * 10;
            if( Race.RaceList[mobTemplate.Race].Coins )
            {
                int level = mobTemplate.Level;
                mob.ReceiveCopper( MUDMath.Dice( 12, level ) / 32 );
                mob.ReceiveSilver( MUDMath.Dice( 9, level ) / 32 );
                mob.ReceiveGold( MUDMath.Dice( 5, level ) / 32 );
                mob.ReceivePlatinum( MUDMath.Dice( 2, level ) / 32 );
            }
            else
            {
                mob.SetCoins( 0, 0, 0, 0 );
            }
            mob.ArmorPoints = MUDMath.Interpolate( mob.Level, 100, -100 );

            // * MOB HITPOINTS *
            //
            // Was level d 8, upped it to level d 13
            // considering mobs *still* won't have as many hitpoints as some players until
            // at least level 10, this shouldn't be too big an upgrade.
            //
            // Mob hitpoints are not based on constitution *unless* they have a
            // constitution modifier from an item, spell, or other affect

            // In light of recent player dissatisfaction with the
            // mob hitpoints, I'm implementing a log curve, using
            //  hp = exp( 2.15135 + level*0.151231)
            // This will will result in the following hp matrix:
            //     Level    Hitpoints
            //      20        175
            //      30        803
            //      40        3643
            //      50        16528
            //      55        35207
            //      60        75000
            mob.MaxHitpoints = MUDMath.Dice( mob.Level, 13 ) + 1;
            // Mob hps are non-linear above level 10.
            if( mob.Level > 20 )
            {
                int upper = (int)Math.Exp( 1.85 + mob.Level * 0.151231 );
                int lower = (int)Math.Exp( 1.80 + mob.Level * 0.151231 );
                mob.MaxHitpoints += MUDMath.NumberRange( lower, upper );
            }
            else if (mob.Level > 10)
            {
                mob.MaxHitpoints += MUDMath.NumberRange(mob.Level * 2, ((mob.Level - 8) ^ 2 * mob.Level) / 2);
            }

            // Demons/devils/dragons gain an extra 30 hitpoints per level (+1500 at lvl 50).
            if (mob.GetRace() == Race.RACE_DEMON || mob.GetRace() == Race.RACE_DEVIL || mob.GetRace() == Race.RACE_DRAGON)
            {
                mob.MaxHitpoints += (mob.Level * 30);
            }

            mob.Hitpoints = mob.GetMaxHit();

            // Horses get more moves, necessary for mounts.
            if(Race.RaceList[ mob.GetRace() ].Name.Equals( "Horse", StringComparison.CurrentCultureIgnoreCase ))
            {
                mob.MaxMoves = 290 + MUDMath.Dice( 4, 5 );
                mob.CurrentMoves = mob.MaxMoves;
            }
            mob.LoadRoomIndexNumber = 0;

            // Insert in list.
            CharList.Add( mob );
            // Increment count of in-game instances of mob.
            mobTemplate.NumActive++;
            return mob;
        }

        /// <summary>
        /// Creates a duplicate of a mobile minus its inventory.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="clone"></param>
        public static void CloneMobile( CharData parent, CharData clone )
        {
            int i;

            if( parent == null || clone == null || !parent.IsNPC() )
                return;

            // Fix values.
            clone.Name = parent.Name;
            clone.ShortDescription = parent.ShortDescription;
            clone.FullDescription = parent.FullDescription;
            clone.Description = parent.Description;
            clone.Gender = parent.Gender;
            clone.CharacterClass = parent.CharacterClass;
            clone.SetPermRace( parent.GetRace() );
            clone.Level = parent.Level;
            clone.TrustLevel = 0;
            clone.SpecialFunction = parent.SpecialFunction;
            clone.SpecialFunctionNames = parent.SpecialFunctionNames;
            clone.Timer = parent.Timer;
            clone.Wait = parent.Wait;
            clone.Hitpoints = parent.Hitpoints;
            clone.MaxHitpoints = parent.MaxHitpoints;
            clone.CurrentMana = parent.CurrentMana;
            clone.MaxMana = parent.MaxMana;
            clone.CurrentMoves = parent.CurrentMoves;
            clone.MaxMoves = parent.MaxMoves;
            clone.SetCoins( parent.GetCopper(), parent.GetSilver(), parent.GetGold(), parent.GetPlatinum() );
            clone.ExperiencePoints = parent.ExperiencePoints;
            clone.ActionFlags = parent.ActionFlags;
            clone.Affected = parent.Affected;
            clone.CurrentPosition = parent.CurrentPosition;
            clone.Alignment = parent.Alignment;
            clone.Hitroll = parent.Hitroll;
            clone.Damroll = parent.Damroll;
            clone.Wimpy = parent.Wimpy;
            clone.Deaf = parent.Deaf;
            clone.Hunting = parent.Hunting;
            clone.Hating = parent.Hating;
            clone.Fearing = parent.Fearing;
            clone.Resistant = parent.Resistant;
            clone.Immune = parent.Immune;
            clone.Susceptible = parent.Susceptible;
            clone.CurrentSize = parent.CurrentSize;
            clone.PermStrength = parent.PermStrength;
            clone.PermIntelligence = parent.PermIntelligence;
            clone.PermWisdom = parent.PermWisdom;
            clone.PermDexterity = parent.PermDexterity;
            clone.PermConstitution = parent.PermConstitution;
            clone.PermAgility = parent.PermAgility;
            clone.PermCharisma = parent.PermCharisma;
            clone.PermPower = parent.PermPower;
            clone.PermLuck = parent.PermLuck;
            clone.ModifiedStrength = parent.ModifiedStrength;
            clone.ModifiedIntelligence = parent.ModifiedIntelligence;
            clone.ModifiedWisdom = parent.ModifiedWisdom;
            clone.ModifiedDexterity = parent.ModifiedDexterity;
            clone.ModifiedConstitution = parent.ModifiedConstitution;
            clone.ModifiedAgility = parent.ModifiedAgility;
            clone.ModifiedCharisma = parent.ModifiedCharisma;
            clone.ModifiedPower = parent.ModifiedPower;
            clone.ModifiedLuck = parent.ModifiedLuck;
            clone.ArmorPoints = parent.ArmorPoints;
            //clone._mpactnum = parent._mpactnum;

            for (i = 0; i < 6; i++)
            {
                clone.SavingThrows[i] = parent.SavingThrows[i];
            }

            // Now add the affects.
            foreach (Affect affect in parent.Affected)
            {
                clone.AddAffect(affect);
            }
        }

        /// <summary>
        /// Create an instance of an object.
        /// </summary>
        /// <param name="objTempalte"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static Object CreateObject( ObjTemplate objTempalte, int level )
        {
            if( level < 1 )
            {
                level = 1;
            }

            if( !objTempalte )
            {
                Log.Error("CreateObject: null ObjTemplate.", 0);
                return null;
            }

            Object obj = new Object( objTempalte );
            if( !obj )
            {
                Log.Error("Database.CreateObject: new Object(ObjIndex*) failed.", 0);
                return null;
            }
            obj.Level = level;
            return obj;
        }

        /// <summary>
        /// Duplicate an object exactly minus contents.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="clone"></param>
        public static void CloneObject( Object parent, ref Object clone )
        {
            int i;
            ExtendedDescription edNew;

            if( parent == null || clone == null )
                return;

            // Start fixing the object.
            clone.Name = parent.Name;
            clone.ShortDescription = parent.ShortDescription;
            clone.FullDescription = parent.FullDescription;
            clone.SpecFun = parent.SpecFun;
            clone.Affected = parent.Affected;
            clone.ItemType = parent.ItemType;
            clone.WearFlags = parent.WearFlags;
            clone.UseFlags = parent.UseFlags;
            clone.Material = parent.Material;
            clone.Size = parent.Size;
            clone.Volume = parent.Volume;
            clone.Craftsmanship = parent.Craftsmanship;
            clone.Weight = parent.Weight;
            clone.Cost = parent.Cost;
            clone.Level = parent.Level;
            clone.Condition = parent.Condition;
            clone.Timer = parent.Timer;
            clone.Condition = parent.Condition;
            clone.Trap = parent.Trap;

            for( i = 0; i < Limits.NUM_ITEM_EXTRA_VECTORS; i++ )
                clone.ExtraFlags[ i ] = parent.ExtraFlags[ i ];

            for( i = 0; i < Limits.NUM_AFFECT_VECTORS; i++ )
                clone.AffectedBy[ i ] = parent.AffectedBy[ i ];

            for( i = 0; i < 8; i++ )
                clone.Values[ i ] = parent.Values[ i ];

            /* extended desc */
            foreach( ExtendedDescription ed in parent.ExtraDescription )
            {
                edNew = new ExtendedDescription();
                edNew.Keyword = ed.Keyword;
                edNew.Description = ed.Description;
                clone.ExtraDescription.Add( edNew );
            }
        }

        /// <summary>
        /// Get an extra description from a list.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ed"></param>
        /// <returns></returns>
        public static string GetExtraDescription( string name, List<ExtendedDescription> ed )
        {
            if (String.IsNullOrEmpty(name) || ed == null || ed.Count < 1)
            {
                return String.Empty;
            }

            foreach( ExtendedDescription desc in ed )
            {
                if( MUDString.NameContainedIn( name, desc.Keyword ) )
                {
                    return ( desc.Description );
                }
            }
            return String.Empty;
        }

        /// <summary>
        /// Gets a mob template class based on the supplied virtual number.
        /// </summary>
        /// <param name="indexNumber"></param>
        /// <returns></returns>
        public static MobTemplate GetMobTemplate( int indexNumber )
        {
            // No indexNumber = give up.  We also can't have dupes if there are less than two mobs.
            if( indexNumber < 0 || MobTemplate.Count <= 1 )
            {
                return null;
            }

            foreach( Area area in AreaList )
            {
                foreach( MobTemplate mob in area.Mobs )
                {
                    if( mob.IndexNumber == indexNumber )
                    {
                        return mob;
                    }
                }
            }

            if( DatabaseIsBooting )
            {
                Log.Error("GetMobTemplate: bad indexNumber " + indexNumber);
                throw new NullReferenceException();
            }
            return null;
        }

        /// <summary>
        /// Gets an object template based on a virtual number.
        /// </summary>
        /// <param name="indexNumber"></param>
        /// <returns></returns>
        public static ObjTemplate GetObjTemplate( int indexNumber )
        {
            // There is a possibility of indexNumber passed is negative.
            if( indexNumber < 0 )
            {
                return null;
            }

            foreach( Area area in AreaList )
            {
                foreach( ObjTemplate obj in area.Objects )
                {
                    if( obj.IndexNumber == indexNumber )
                    {
                        return obj;
                    }
                }
            }

            if( DatabaseIsBooting )
            {
                Log.Error("Database.GetObjTemplate: bad indexNumber " + indexNumber);
                Log.Trace( "FIX THIS!  IT WILL CAUSE PROBLEMS!" );
            }

            return null;
        }

        /// <summary>
        /// Generate a random door.
        /// </summary>
        /// <returns></returns>
        public static Exit.Direction RandomDoor()
        {
            return (Exit.Direction)MUDMath.NumberRange(0, (Enum.GetValues(typeof(Exit.Direction)).Length));
        }

        /// <summary>
        /// Appends a string to a file.
        /// </summary>
        /// <param name="ch"></param>
        /// <param name="file"></param>
        /// <param name="str"></param>
        public static void AppendFile( CharData ch, string file, string str )
        {
            if( ch == null || String.IsNullOrEmpty(file) || String.IsNullOrEmpty(str) || ch.IsNPC() )
            {
                return;
            }
            FileStream fp = File.OpenWrite( file );
            StreamWriter sw = new StreamWriter( fp );
            sw.WriteLine( "[{0}] {1}: {2}\n", ch.InRoom ? MUDString.PadInt(ch.InRoom.IndexNumber,5) : MUDString.PadInt(0,5),
                ch.Name, str );
            sw.Flush();
            sw.Close();
            return;
        }

        /// <summary>
        /// Logs a message to the guild log file.
        /// </summary>
        /// <param name="str"></param>
        public static void LogGuild( string str )
        {
            Log.Trace("GUILD: ");
        }

        /// <summary>
        /// Checks whether a particular indexNumber is flagged as an artifact.
        /// </summary>
        /// <param name="indexNumber"></param>
        /// <returns></returns>
        public static bool IsArtifact( int indexNumber )
        {
            ObjTemplate obj = GetObjTemplate( indexNumber );
            if (obj != null && Macros.IsSet(obj.ExtraFlags[ObjTemplate.ITEM_ARTIFACT.Group], ObjTemplate.ITEM_ARTIFACT.Vector))
            {
                return true;
            }

            return false;
        }

    }
}