﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4a2s
{
    static class Index
    {
        private static Hashtable _hashtable;
        private static int _numSongs;

        private static string _songGuids;
        private static string _bankGuids;
        private static string _waveGuids;
        private static string _gbwaveGuids;

        public static void IndexRom()
        {
            /*
             * create a new Hashtable instance where all our entities will end up
             */
            _hashtable = new Hashtable();
            _numSongs = GetNumSongs();
            /*
             * now we will index all our songs
             */

            Rom.Reader.BaseStream.Position = Rom.SongtableOffset;

            int numVoicegroups = 0;
            int numSamples = 0;
            int numWaves = 0;

            for (int i = 0; i < _numSongs; i++)
            {
                // hash all songs
                Rom.Reader.BaseStream.Position = Rom.SongtableOffset + (i*8);
                int currentSongPointer = Rom.Reader.ReadInt32() - Rom.Map;
                Rom.Reader.ReadInt32(); // drop the two song group bytes x2 cuz we don't need 'em
                if (!_hashtable.Contains(currentSongPointer))
                    _hashtable.Add(currentSongPointer, new Entity(EntityType.Song, currentSongPointer, _songGuids + "_" + i.ToString("D3"), -1));
                // now hash the voicegroup
                Rom.Reader.BaseStream.Position = currentSongPointer;
                Rom.Reader.BaseStream.Position += 4;
                int voicegroupOffset = Rom.Reader.ReadInt32() - Rom.Map;
                if (!_hashtable.Contains(voicegroupOffset))
                    _hashtable.Add(voicegroupOffset, new Entity(EntityType.Bank, voicegroupOffset, _bankGuids + "_" + numVoicegroups++.ToString("D3"), 128));

                for (int i = 0; i < 128; i++) // 128 = amount of instruments in voicegroup
                {
                    Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i); // 12 = length of instrument declaration
                    uint instrumentType = Rom.Reader.ReadUInt32();
                    instrumentType &= 0xFF;  // cut off unimportant information
                    if (instrumentType == 0x0 || instrumentType == 0x8 || instrumentType == 0x10 || instrumentType == 0x18)
                    {
                        /*
                         * This seems to be a direct instrument declaration, let's collect the wave sample in our hashtable
                         */
                        Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i) + 4;
                        int samplePointer = Rom.Reader.ReadInt32() - Rom.Map;
                        if (!_hashtable.Contains(samplePointer))
                            _hashtable.Add(samplePointer, new Entity(EntityType.Wave, samplePointer, _waveGuids + "_" + numSamples++.ToString("D3"), -1));
                    }
                    else if (instrumentType == 0x03 || instrumentType == 0xB)
                    {
                        Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i) + 4;
                        int wavePointer = Rom.Reader.ReadInt32() - Rom.Map;
                        if (!_hashtable.Contains(wavePointer))
                            _hashtable.Add(wavePointer, new Entity(EntityType.GbWave, wavePointer, _gbwaveGuids + "_" + numWaves++.ToString("D3"), -1));
                    }
                    else if (instrumentType == 0x40)
                    {
                        Rom.Reader.BaseStream.Position = voicegroupOffset + (12 * i) + 4;
                        int subInstruments = Rom.Reader.ReadInt32() - Rom.Map;
                        int keyMapTable = Rom.Reader.ReadInt32() - Rom.Map;
                        // TODO
                    }
                    else if (instrumentType == 0x80)
                    {
                        Rom.Reader.BaseStream.Position = voicegroupOffset + (12 * i) + 4;
                        int subVoicegroup = Rom.Reader.ReadInt32() - Rom.Map;
                        // TODO
                    }
                }
            }

            /*
             * Now we should've put everything in the hashtable without duplicates
             */
        }

        private static bool IsValidSong(int songNum)
        {
            /*
             * First we need to make sure we seek to the position where the song is located
             * After that we save song header information into variables
             */
            Rom.Reader.BaseStream.Position = Rom.SongtableOffset + (songNum*8);
            Rom.Reader.BaseStream.Position = Rom.Reader.ReadInt32() - Rom.Map;
            byte numTracks = Rom.Reader.ReadByte();
            byte numBlocks = Rom.Reader.ReadByte();
            byte priority = Rom.Reader.ReadByte();
            byte reverb = Rom.Reader.ReadByte();

            int voicegroupOffset = Rom.Reader.ReadInt32();
            /*
             * the 3 lower bytes aren't needed for the verification but we might use it in the *future*
             */

            if (!IsValidPointer(voicegroupOffset)) return false;

            for (int i = 0; i < numTracks; i++)
            {
                if (!IsValidPointer(Rom.Reader.ReadInt32())) return false;
            }

            return true;
        }

        private static bool IsValidPointer(int pointer)
        {
            if (pointer - Rom.Map < 0 || pointer - Rom.Map > Rom.RomSize) return false;
            return true;
        }

        private static int GetNumSongs()
        {
            int numSongs = 0;

            while (IsValidSong(numSongs)) numSongs++;
            return numSongs;
        }

        private static int GetNumInstrumentsByKeyMap(int keyMapPointer)
        {
            Rom.Reader.BaseStream.Position = keyMapPointer;
            byte biggestInstrument = 0;
            for (int i = 0; i < 128; i++)
            {
                byte mapping = Rom.Reader.ReadByte();
                if (mapping > biggestInstrument && mapping < 128) biggestInstrument = mapping;
            }
            return biggestInstrument;
        }
    }
}