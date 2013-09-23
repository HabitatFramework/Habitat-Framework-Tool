﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using HLU.Data;
using HLU.Data.Model;

namespace HLU.UI.ViewModel
{
    class ViewModelWindowMainUpdate
    {
        ViewModelWindowMain _viewModelMain;

        public ViewModelWindowMainUpdate(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        /// <summary>
        /// Writes changes made to current incid back to database and GIS layer.
        /// Also synchronizes shadow copy of GIS layer in DB and writes history.
        /// </summary>
        internal bool Update()
        {
            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            try
            {
                _viewModelMain.ChangeCursor(Cursors.Wait, "Saving ...");

                int incidCurrRowIx = _viewModelMain.IncidCurrentRowIndex;

                if (_viewModelMain.IsDirtyIncid())
                {
                    IncidCurrentRowDerivedValuesUpdate(_viewModelMain);

                    _viewModelMain.IncidCurrentRow.last_modified_date = DateTime.Now;
                    _viewModelMain.IncidCurrentRow.last_modified_user_id = _viewModelMain.UserID;

                    if (_viewModelMain.HluTableAdapterManager.incidTableAdapter.Update(
                        (HluDataSet.incidDataTable)_viewModelMain.HluDataset.incid.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update '{0}' table.",
                            _viewModelMain.HluDataset.incid.TableName));
                }

                if ((_viewModelMain.IncidIhsMatrixRows != null) && _viewModelMain.IsDirtyIncidIhsMatrix())
                {
                    if (_viewModelMain.HluTableAdapterManager.incid_ihs_matrixTableAdapter.Update(
                        (HluDataSet.incid_ihs_matrixDataTable)_viewModelMain.HluDataset.incid_ihs_matrix.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update '{0}' table.",
                            _viewModelMain.HluDataset.incid_ihs_matrix.TableName));
                }

                if ((_viewModelMain.IncidIhsFormationRows != null) && _viewModelMain.IsDirtyIncidIhsFormation())
                {
                    if (_viewModelMain.HluTableAdapterManager.incid_ihs_formationTableAdapter.Update(
                        (HluDataSet.incid_ihs_formationDataTable)_viewModelMain.HluDataset.incid_ihs_formation.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update '{0}' table.",
                            _viewModelMain.HluDataset.incid_ihs_formation.TableName));
                }

                if ((_viewModelMain.IncidIhsManagementRows != null) && _viewModelMain.IsDirtyIncidIhsManagement())
                {
                    if (_viewModelMain.HluTableAdapterManager.incid_ihs_managementTableAdapter.Update(
                        (HluDataSet.incid_ihs_managementDataTable)_viewModelMain.HluDataset.incid_ihs_management.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update '{0}' table.",
                            _viewModelMain.HluDataset.incid_ihs_management.TableName));
                }

                if ((_viewModelMain.IncidIhsComplexRows != null) && _viewModelMain.IsDirtyIncidIhsComplex())
                {
                    if (_viewModelMain.HluTableAdapterManager.incid_ihs_complexTableAdapter.Update(
                        (HluDataSet.incid_ihs_complexDataTable)_viewModelMain.HluDataset.incid_ihs_complex.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update '{0}' table.",
                            _viewModelMain.HluDataset.incid_ihs_complex.TableName));
                }

                if (_viewModelMain.IsDirtyIncidBap()) UpdateBap();

                if (_viewModelMain.IncidSourcesRows != null)
                {
                    int j = 0;
                    for (int i = 0; i < _viewModelMain.IncidSourcesRows.Length; i++)
                        if (_viewModelMain.IncidSourcesRows[i] != null)
                            _viewModelMain.IncidSourcesRows[i].sort_order = ++j;

                    if (_viewModelMain.HluTableAdapterManager.incid_sourcesTableAdapter.Update(
                        (HluDataSet.incid_sourcesDataTable)_viewModelMain.IncidSourcesTable.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update {0} table.", 
                            _viewModelMain.HluDataset.incid_sources.TableName));
                }

                // update all GIS rows corresponding to this incid
                List<SqlFilterCondition> incidCond = new List<SqlFilterCondition>(new SqlFilterCondition[] { 
                    new SqlFilterCondition(_viewModelMain.HluDataset.incid_mm_polygons, 
                        _viewModelMain.HluDataset.incid_mm_polygons.incidColumn, _viewModelMain.Incid) });

                var q = _viewModelMain.HluDataset.lut_ihs_habitat
                    .Where(r => r.code == _viewModelMain.IncidCurrentRow.ihs_habitat);
                string ihsHabitatCategory = q.Count() == 1 ? q.ElementAt(0).category : null;

                DataTable historyTable = _viewModelMain.GISApplication.UpdateFeatures(new DataColumn[] { 
                    _viewModelMain.HluDataset.incid_mm_polygons.ihs_categoryColumn,
                    _viewModelMain.HluDataset.incid_mm_polygons.ihs_summaryColumn },
                    new object[] { ihsHabitatCategory, _viewModelMain.IncidIhsSummary },
                    _viewModelMain.HistoryColumns, incidCond);

                if (historyTable == null)
                    throw new Exception("Error updating GIS layer.");
                else if (historyTable.Rows.Count == 0)
                    throw new Exception("No GIS features to update.");

                // likewise update DB shadow copy of GIS layer
                if (_viewModelMain.DataBase.ExecuteNonQuery(String.Format("UPDATE {0} SET {1} = {3}, {2} = {4} WHERE {5}",
                    _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName),
                    _viewModelMain.DataBase.QuoteIdentifier(
                        _viewModelMain.HluDataset.incid_mm_polygons.ihs_categoryColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteIdentifier(
                        _viewModelMain.HluDataset.incid_mm_polygons.ihs_summaryColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteValue(ihsHabitatCategory),
                    _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidIhsSummary),
                    _viewModelMain.DataBase.WhereClause(false, true, true, incidCond)),
                    _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text) == -1)
                    throw new Exception("Failed to update database copy of GIS layer.");

                // save history returned from GIS
                Dictionary<int, string> fixedValues = new Dictionary<int, string>();
                fixedValues.Add(_viewModelMain.HluDataset.history.incidColumn.Ordinal, _viewModelMain.Incid);
                ViewModelWindowMainHistory vmHist = new ViewModelWindowMainHistory(_viewModelMain);
                vmHist.HistoryWrite(fixedValues, historyTable, ViewModelWindowMain.Operations.AttributeUpdate);

                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();
                _viewModelMain.Saved = true;

                _viewModelMain.IncidRowCount(true);
                _viewModelMain.IncidCurrentRowIndex = incidCurrRowIx;

                return true;
            }
            catch (Exception ex)
            {
                _viewModelMain.DataBase.RollbackTransaction();
                if (_viewModelMain.HaveGisApp)
                {
                    _viewModelMain.Saved = false;
                    MessageBox.Show("Your changes could not be saved. The error message returned was:\n\n" +
                        ex.Message, "HLU: Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                else
                {
                    return true;
                }
            }
            finally
            {
                _viewModelMain.SavingAttempted = true;
                _viewModelMain.Saving = false;
                _viewModelMain.ChangeCursor(Cursors.Arrow, null);
            }
        }

        /// <summary>
        /// Updates BAP environment rows corresponding to current incid.
        /// </summary>
        private void UpdateBap()
        {
            if (_viewModelMain.IncidBapRowsAuto == null)
                _viewModelMain.IncidBapRowsAuto = new ObservableCollection<BapEnvironment>();
            if (_viewModelMain.IncidBapHabitatsUser == null)
                _viewModelMain.IncidBapHabitatsUser = new ObservableCollection<BapEnvironment>();

            // remove duplicate codes
            IEnumerable<BapEnvironment> beAuto = from b in _viewModelMain.IncidBapRowsAuto
                                                 group b by b.bap_habitat into habs
                                                 select habs.First();

            IEnumerable<BapEnvironment> beUser = from b in _viewModelMain.IncidBapHabitatsUser
                                                 where beAuto.Count(a => a.bap_habitat == b.bap_habitat) == 0
                                                 group b by b.bap_habitat into habs
                                                 select habs.First();

            var currentBapRows = beAuto.Concat(beUser);

            List<HluDataSet.incid_bapRow> newRows = new List<HluDataSet.incid_bapRow>();
            List<HluDataSet.incid_bapRow> updateRows = new List<HluDataSet.incid_bapRow>();
            HluDataSet.incid_bapRow updateRow;

            foreach (BapEnvironment be in currentBapRows)
            {
                if (be.bap_id == -1) // new BAP environment
                {
                    be.bap_id = _viewModelMain.RecIDs.NextIncidBapId;
                    be.incid = _viewModelMain.Incid;
                    HluDataSet.incid_bapRow newRow = _viewModelMain.IncidBapTable.Newincid_bapRow();
                    newRow.ItemArray = be.ToItemArray();
                    newRows.Add(newRow);
                }
                else if ((updateRow = UpdateIncidBapRow(be)) != null)
                {
                    updateRows.Add(updateRow);
                }
            }

            _viewModelMain.IncidBapRows.Where(r => r.RowState != DataRowState.Deleted &&
                currentBapRows.Count(g => g.bap_id == r.bap_id) == 0).ToList()
                .ForEach(delegate(HluDataSet.incid_bapRow row) { row.Delete(); });
            
            if (_viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Update(
                _viewModelMain.IncidBapRows.Where(r => r.RowState == DataRowState.Deleted).ToArray()) == -1)
                throw new Exception(String.Format("Failed to update {0} table.", _viewModelMain.HluDataset.incid_bap.TableName));

            if (updateRows.Count > 0)
            {
                if (_viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Update(updateRows.ToArray()) == -1)
                    throw new Exception(String.Format("Failed to update {0} table.", _viewModelMain.HluDataset.incid_bap.TableName));
            }

            foreach (HluDataSet.incid_bapRow r in newRows)
                _viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Insert(r);
        }

        /// <summary>
        /// Writes the values from a BapEnvironment object bound to the BAP data grids into the corresponding incid_bap DataRow.
        /// </summary>
        /// <param name="be">BapEnvironment object bound to data grid on form.</param>
        /// <returns>Updated incid_bap row, or null if no corresponding row was found.</returns>
        private HluDataSet.incid_bapRow UpdateIncidBapRow(BapEnvironment be)
        {
            var q = _viewModelMain.IncidBapRows.Where(r => r.RowState != DataRowState.Deleted && r.bap_id == be.bap_id);
            if (q.Count() == 1)
            {
                if (!be.IsValid()) return null;
                HluDataSet.incid_bapRow oldRow = q.ElementAt(0);
                object[] itemArray = be.ToItemArray();
                for (int i = 0; i < itemArray.Length; i++)
                    oldRow[i] = itemArray[i];
                return oldRow;
            }
            return null;
        }

        /// <summary>
        /// Updates those columns of IncidCurrentRow in main view model that are not directly updated 
        /// by properties (to enable undo if update cancelled).
        /// Called here and by bulk update.
        /// </summary>
        /// <param name="viewModelMain">Reference to main window view model.</param>
        internal static void IncidCurrentRowDerivedValuesUpdate(ViewModelWindowMain viewModelMain)
        {
            viewModelMain.IncidCurrentRow.ihs_habitat = viewModelMain.IncidIhsHabitat;
            viewModelMain.IncidCurrentRow.last_modified_user_id = viewModelMain.IncidLastModifiedUser;
            viewModelMain.IncidCurrentRow.last_modified_date = viewModelMain.IncidLastModifiedDateVal;
        }

        internal void UpdateIncidModifiedColumns(string incid)
        {
            if (_viewModelMain.DataBase.ExecuteNonQuery(String.Format("UPDATE {0} SET {1} = {2}, {3} = {4} WHERE {5} = {6}",
                _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.last_modified_dateColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(DateTime.Today),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.last_modified_user_idColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(_viewModelMain.UserID),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(incid)),
                _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text) == -1)
                throw new Exception("Failed to update history table.");
        }
    }
}