// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

static class SampleColors
{
    public static readonly Color32 Gray
        = new Color32(0x8B, 0x8C, 0x8E, 0xFF);

    public static readonly Color32 Green
        = new Color32(0x5A, 0xCA, 0x25, 0xFF);

    public static readonly Color32 Red
        = new Color32(0xCA, 0x26, 0x22, 0xFF);

    public static readonly Color32 Yellow
        = new Color32(0xFF, 0xEB, 0x04, 0xFF);

    public static readonly Color32 Noice
        = new Color32(0x80, 0xFF, 0x87, 0xFF);

    public static readonly Color32 Warn
        = new Color32(0xF0, 0x96, 0x06, 0xFF);

    public static readonly Color32 Alert
        = new Color32(0xF0, 0x60, 0x80, 0xFF);


    public static bool GetLogTag(LogType type, out string tag)
    {
        switch (type)
        {
            case LogType.Warning:
                tag = RichText.Yellow;
                return true;
            case LogType.Error:
            case LogType.Exception:
                tag = RichText.Red;
                return true;
            case LogType.Assert:
                tag = RichText.Alert;
                return true;
        }

        tag = string.Empty;
        return false;
    }


    public static class RichText
    {
        // ReSharper disable MemberHidesStaticFromOuterClass

        public static readonly string Gray = Convert(SampleColors.Gray);

        public static readonly string Green = Convert(SampleColors.Green);

        public static readonly string Red = Convert(SampleColors.Red);

        public static readonly string Yellow = Convert(SampleColors.Yellow);

        public static readonly string Noice = Convert(SampleColors.Noice);

        public static readonly string Warn = Convert(SampleColors.Warn);

        public static readonly string Alert = Convert(SampleColors.Alert);

        // ReSharper restore MemberHidesStaticFromOuterClass

        public static string Convert(Color32 c)
        {
            return $"<color=#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}>";
        }

    } // end nested static class RichText

} // end static class SampleColors
