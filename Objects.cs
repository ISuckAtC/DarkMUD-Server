using System;
using System.Collections.Generic;

namespace Objects
{
    public class Coordinate
    {
        public int x, y;
        public Coordinate(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
    public class Player
    {
        public string username, password;

        public Coordinate position;

        public bool admin = false;

        public Player(string username, string password, Coordinate position)
        {
            this.username = username;
            this.password = password;
            this.position = position;
        }
    }

    public class Tile
    {
        public string description;

        public bool n, s, w, e;

        public Tile(string description = "Test description", bool n = false, bool s = false, bool w = false, bool e = false)
        {
            this.description = description;
            this.n = n;
            this.s = s;
            this.w = w;
            this.e = e;
        }

        public string Examine()
        {
            string examine = description;

            examine += "\nExits: " + (!(n || s || e || w) ? "None" : "") + (n ? "North" + (s || e || w ? " : " : "") : "") + (s ? "South" + (e || w ? " : " : "") : "") + (e ? "East" + (w ? " : " : "") : "") + (w ? "West" : "") + ".";

            return examine;
        }
    }

    public static class Methods
    {
        public static T[,] InitiateCollection<T>(this T[,] arr, Func<T> init)
        {
            for (int x = 0; x < arr.GetLength(0); ++x) for (int y = 0; y < arr.GetLength(1); ++y)
                {
                    arr[x, y] = init();
                }
            return arr;
        }

        public static string Namelist(this List<Player> players)
        {
            string s = "";
            for (int x = 0; x < players.Count; ++x) s += (players[x].admin ? "[A] " : "") + players[x].username + "\n";
            return s;
        }
    }
}