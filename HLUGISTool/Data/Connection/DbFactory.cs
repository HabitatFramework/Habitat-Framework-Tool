﻿using System;
using System.Windows;
using HLU.Properties;
using HLU.UI.View.Connection;
using HLU.UI.ViewModel;

namespace HLU.Data.Connection
{
    class DbFactory
    {
        private static ViewSelectConnection _selConnWindow;
        private static ViewModelSelectConnection _selConnViewModel;
        private static ConnectionTypes _connType;

        public static DbBase CreateConnection()
        {
            if (Enum.IsDefined(typeof(ConnectionTypes), Settings.Default.DbConnectionType))
                _connType = (ConnectionTypes)Settings.Default.DbConnectionType;
            else
                _connType = ConnectionTypes.Unknown;
            
            string connString = Settings.Default.DbConnectionString;
            string defaultSchema = Settings.Default.DbDefaultSchema;
            bool promptPwd = Settings.Default.DbPromptPwd;

            if ((_connType == ConnectionTypes.Unknown) || String.IsNullOrEmpty(connString) ||
                ((DbBase.GetBackend(connString, _connType) != Backends.Access) && String.IsNullOrEmpty(defaultSchema)))
            {
                promptPwd = false;
                SelectConnectionType();
            }
            
            if (_connType == ConnectionTypes.Unknown) return null;

            DbBase db = null;

            switch (_connType)
            {
                case ConnectionTypes.ODBC:
                    db = new DbOdbc(ref connString, ref defaultSchema, ref promptPwd,
                        Properties.Resources.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders, 
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, 
                        Settings.Default.DbTextLength, Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision, 
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale);
                    break;
                case ConnectionTypes.OleDb:
                    db = new DbOleDb(ref connString, ref defaultSchema, ref promptPwd,
                        Properties.Resources.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders, 
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, Settings.Default.DbTextLength,
                        Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale);
                    break;
                case ConnectionTypes.Oracle:
                    db = new DbOracle(ref connString, ref defaultSchema, ref promptPwd,
                        Properties.Resources.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, Settings.Default.DbTextLength,
                        Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale);
                    break;
                case ConnectionTypes.PostgreSQL:
                    db = new DbPgSql(ref connString, ref defaultSchema, ref promptPwd,
                        Properties.Resources.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, Settings.Default.DbTextLength,
                        Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale);
                    break;
                case ConnectionTypes.SQLServer:
                    db = new DbSqlServer(ref connString, ref defaultSchema, ref promptPwd,
                        Properties.Resources.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, Settings.Default.DbTextLength,
                        Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale);
                    break;
            }

            if (db != null)
            {
                Settings.Default.DbConnectionType = (int)_connType;
                Settings.Default.DbConnectionString = connString;
                Settings.Default.DbDefaultSchema = defaultSchema;
                Settings.Default.DbPromptPwd = promptPwd;
                Settings.Default.Save();
            }

            return db;
        }

        public static bool ClearSettings()
        {
            try
            {
                Settings.Default.DbConnectionType = (int)ConnectionTypes.Unknown;
                Settings.Default.DbConnectionString = String.Empty;
                Settings.Default.DbDefaultSchema = string.Empty;
                Settings.Default.DbPromptPwd = true;
                Settings.Default.Save();

                return true;
            }
            catch { return false; }
        }

        private static ConnectionTypes SelectConnectionType()
        {
            try
            {
                _selConnWindow = new ViewSelectConnection();
                if ((_selConnWindow.Owner = App.GetActiveWindow()) == null)
                    throw (new Exception("No parent window loaded"));

                // create ViewModel to which main window binds
                _selConnViewModel = new ViewModelSelectConnection();
                _selConnViewModel.DisplayName = "Connection Type";

                // when ViewModel asks to be closed, close window
                _selConnViewModel.RequestClose +=
                    new ViewModelSelectConnection.RequestCloseEventHandler(_selConnViewModel_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _selConnWindow.DataContext = _selConnViewModel;

                _selConnWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _selConnWindow.Topmost = true;

                // show window
                _selConnWindow.ShowDialog();

                return _connType;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connection Type", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return ConnectionTypes.Unknown;
        }

        private static void _selConnViewModel_RequestClose(ConnectionTypes connType, string errorMsg)
        {
            _selConnViewModel.RequestClose -= _selConnViewModel_RequestClose;
            _selConnWindow.Close();

            if (!String.IsNullOrEmpty(errorMsg))
                MessageBox.Show(errorMsg, "Connection Type", MessageBoxButton.OK, MessageBoxImage.Error);

            _connType = connType;
        }
    }
}