using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;

using System.Configuration;

namespace Lab2c
{
    public partial class Form1 : Form
    {
        DataSet myDataSet = new DataSet();
        BindingSource bindingSourceDGV1 = new BindingSource();
        BindingSource bindingSourceDGV2 = new BindingSource();
        DataRelation relation;
        SqlCommandBuilder cb;
        SqlCommand updateCmd;
        SqlCommand insertCmd;
        SqlCommand deleteCmd;
        SqlDataAdapter adapter = new SqlDataAdapter();
        SqlDataAdapter childAdapter = new SqlDataAdapter();


        string connectionString;
        string parentTableName;
        string childTableName;

        string parentPK;
        string childPK;
        string childFK;

        List<Label> labelList = new List<Label>();
        List<TextBox> textBoxList = new List<TextBox>();

        public Form1()
        {
            InitializeComponent();
            getConfigurationData();
            disableButtons();
            //disableTextBoxes();
        }

        private void disableButtons()
        {
            updateBtn.Enabled = false;
            deleteBtn.Enabled = false;
            insertBtn.Enabled = false;

        }
         //Clears all the textBoxes for an easier use
         private void enableTextBoxes()
         {
            foreach( TextBox textBox in textBoxList)
            {
                textBox.Enabled = true;
            }
         }

         private void disableTextBoxes()
         {
            foreach (TextBox textBox in textBoxList)
            {
                textBox.Enabled = false;    
            }
         }
         
        private void clearTextBoxes()
         {
            foreach(TextBox textBox in textBoxList)
            {
                textBox.Clear();
            }
         }


         //helps setting some boundaries for the User
         //If the User wants to add a child to the parent ( action that happens when clicking on a row in dataGridView1 ) then the parentID TextBox should be readOnly + the galleryID TextBox should be filled in by the User
         //If the User wants to update or delete a child ( action that happens when clicking on a row in dataGridView2 ) then the childID  TextBox should remain the same --> readOnly + the galleryID TextBox should remain the same
         private void toggleTextBoxAccessibility(string msg)
         {

            //when clicking on the dataGridView1(parent table) there is only the posibility to insert another child in the childTable
             if (msg == "dataGridView1")
             {
                 foreach( TextBox textBox in textBoxList)
                {
                    if (textBox.Name == "idParent")
                    {
                        textBox.ReadOnly = true;
                    } 
                    else if (textBox.Name == "idChildTB")
                    {
                        textBox.ReadOnly= false;
                    }
                    else if (textBox.Name.ToLower() == "galleryidtb")
                    {
                        textBox.ReadOnly = false;
                    }
                }
                 
             }
            //when clicking the dataGridView2(child table) to modify or delete a child ==> idChildTB and galleryIDTB should be changeable
            //not parent ID though
            else if (msg == "dataGridView2")
            {
                foreach (TextBox textBox in textBoxList)
                {
                    if (textBox.Name == "idParentTB")
                    {
                        textBox.ReadOnly = false;
                    }
                    else if (textBox.Name == "idChildTB")
                    {
                        textBox.ReadOnly = true;
                    }
                    else if (textBox.Name.ToLower() == "galleryidtb")
                    {
                        textBox.ReadOnly = true;
                    }
                }
             }
         }

         
         
        private void getConfigurationData()
        {
            connectionString = ConfigurationManager.AppSettings.Get("connectionString");
            parentTableName = ConfigurationManager.AppSettings.Get("parentTable");
            childTableName = ConfigurationManager.AppSettings.Get("childTable");

        }

        //when button clicked with the help of a dataSet load the infos from the database onto the datagridview
        private void connectButton_Click(object sender, EventArgs e)
        {
            //to not have any unexpected surprises disable the update, delete, insert button
            disableButtons();
            myDataSet.Clear();

            using (SqlConnection connection =
            new SqlConnection(connectionString))
            {
                connection.Open();
                // create a SqlDataAdapter for the "Maler" table and use it to fill the dataSet
                SqlDataAdapter parentAdapter = new SqlDataAdapter();
                parentAdapter.SelectCommand = new SqlCommand("SELECT * FROM " + parentTableName, connection);

                //Fill the dataSet with a dataTable named "Maler"
                //Name given to differentiate the two tables that will be in the dataSet
                myDataSet.Clear();
                parentAdapter.Fill(myDataSet, parentTableName);

                // Create a SqlDataAdapter for the "Bilder" table and use it to fill the dataSet
                
                childAdapter.SelectCommand = new SqlCommand("SELECT * FROM " + childTableName, connection);

                //Fill the dataSet with a dataTable named "Bilder"
                //Name given to differentiate the two tables that will be in the dataSet
                childAdapter.Fill(myDataSet, childTableName);
                
                cb = new SqlCommandBuilder(childAdapter);


                //Start building a dataRelation between the two dataTables
                //Get the DataColumn objs from the two DataTables from myDataSet 
                DataColumn parentColumn = new DataColumn();
                DataColumn childColumn = new DataColumn();

                //adapter will fill the dataTable with the Information_Schema keys 
                //AddWithKey will add to the dataTable the missing columns and the primary keys
                parentAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;

                DataTable dt = new DataTable();
                parentAdapter.FillSchema(dt, SchemaType.Source);

                //check each column to see if the column is the primary key
                foreach (DataColumn col in dt.Columns)
                {
                    bool isPrimary = dt.PrimaryKey.Contains(col);
                    if (isPrimary)
                    {
                        parentColumn = myDataSet.Tables[parentTableName].Columns[col.ColumnName];
                        parentPK = col.ColumnName;
                        break;
                    }

                }


                //query to get primary key of child table
                string childPKQuery = "SELECT c.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS t " +
                        "INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE c ON c.Constraint_Name = t.CONSTRAINT_NAME " +
                        "AND c.CONSTRAINT_SCHEMA = t.CONSTRAINT_SCHEMA WHERE t.CONSTRAINT_TYPE = 'Primary Key' AND c.TABLE_NAME =@childTableName";

                //query to get foreign key of child table

                //sys.foreign_keys: Contains a row per object that is a FOREIGN KEY constraint, with sys.object.type = F.
                //sys.foreign_key_columns: Contains a row for each column, or set of columns, that comprise a foreign key.
                //sys.tables: Returns a row for each user table in SQL Server.
                //inner Join firstly sys.foriegn_keys with sys.foreign_key_columns on the foreign-key-columns constraint_obj_id (id of foreign key constraint)
                //object FOREIGN KEY joined with the COLUMN it belongs to
                //next join sys.tables on the previous tables parent_obj_id
                // the previous join is joined now also with the TABLE that the COLUMN with the FOREIGN KEY OBJ belongs to
                //WHERE the parent_obj_id will be the cildTableName and the referenced_obj_id is the parentTableName
                //the referenced_obj_id is the ID which our foreign key has a reference to
                string childFKQuery = "select COL_NAME(fc.parent_object_id, fc.parent_column_id) ColName " +
                    "From sys.foreign_keys as f inner join sys.foreign_key_columns as fc on f.object_id=fc.constraint_object_id " +
                    "inner join sys.tables t on t.object_id= fc.parent_object_id " +
            "where OBJECT_NAME(f.parent_object_id)=@childTable and OBJECT_NAME(f.referenced_object_id)=@parentTable";

                //make a command using query and add its parameters
                SqlCommand getChildPK = new SqlCommand(childPKQuery, connection);
                getChildPK.Parameters.AddWithValue("@childTableName", childTableName);

                SqlCommand getChildFK = new SqlCommand(childFKQuery, connection);
                getChildFK.Parameters.AddWithValue("@childTable", childTableName);
                getChildFK.Parameters.AddWithValue("@parentTable", parentTableName);

                //create dataReaders to read from the output of the Sql queries
                //with .ExecuteReader you get that 
                SqlDataReader readerChildFK = getChildFK.ExecuteReader();
                SqlDataReader readerChildPK = getChildPK.ExecuteReader();

                //read from the SqlDataReader until there's nothing left
                // save the output in strings to retain the names of the childPK and childFK
                while (readerChildPK.Read())
                {
                    childPK = readerChildPK.GetString(0);

                }
                //SqlDataReader must be closed
                readerChildPK.Close();

                while (readerChildFK.Read())
                {
                    childFK = readerChildFK.GetString(0);

                }
                childColumn = myDataSet.Tables[childTableName].Columns[childFK];
                readerChildFK.Close();

                //Build a DataRelation based on those matching columns
                string relationName = parentTableName + "_" + childTableName;
                relation = new DataRelation(relationName, parentColumn, childColumn);

                //try to add the DataRelation to the DataSet
                try
                {
                    myDataSet.Relations.Add(relation);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                //bind the parent dataGridView to the bindingSource

                try
                {
                    cb = new SqlCommandBuilder(childAdapter); 
                    bindingSourceDGV1.DataSource = myDataSet;
                    bindingSourceDGV1.DataMember = parentTableName;
                    dataGridView1.DataSource = bindingSourceDGV1;


                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                createLabels();
                createTextBoxes();
                connection.Close();
            }

        }



        //Show the child tables of the chosen row from the parent dataGridView only when clicking  
        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            
            toggleTextBoxAccessibility("dataGridView1");
            
            clearTextBoxes();
            disableButtons();
            enableTextBoxes();
            
            updateDGV();



            string parentId = dataGridView1.CurrentRow.Cells[parentPK].Value.ToString();

            if (parentId != null)
            {
                //The nr of labels == nr of textboxes
                //Search through the labels to see at which index the parentPK is 
                //When found set the text of the textBox at the same index to the parentId
                for (int i = 0; i < textBoxList.Count; i++)
                {
                    if ( labelList[i].Text.Equals(parentPK)) {
                        
                        textBoxList[i].Text = parentId;
                        textBoxList[i].ReadOnly = true;
                    } 
                }

                this.insertBtn.Enabled = true;

            }
        }

       


        //Gets the values of the row and writes them in their assigned TextBoxes on Cell_Click on the dataGridView of the child Table 
        private void dataGridView2_CellContentClick_1(object sender, DataGridViewCellEventArgs e)
        {
            toggleTextBoxAccessibility("dataGridView2");

            clearTextBoxes();
            if (dataGridView1.SelectedRows.Count > 0)
            {
                if(dataGridView1.SelectedRows != null) 
                {
                    //Go through the list of textBoxes(has the same nr of el as the nr of columns of the child table)
                    //Set the text for each textBox with the afferent column value
                    for (int i = 0; i < textBoxList.Count; i++)
                    {
                        textBoxList[i].Text = dataGridView2.CurrentRow.Cells[i].Value.ToString();
                    }
                }

                /*
                string id = dataGridView2.CurrentRow.Cells[0].Value.ToString();
                string name = dataGridView2.CurrentRow.Cells[1].Value.ToString();
                string malerID = dataGridView2.CurrentRow.Cells[2].Value.ToString();
                string year = dataGridView2.CurrentRow.Cells[3].Value.ToString();
                string galleryID = dataGridView2.CurrentRow.Cells[4].Value.ToString();
                //If id chosen valid -->  fill all the textBoxes with the data from the selected row turned into strings 
                if (id != null)
                {
                    this.idChildTB.Text = id;
                    this.nameTB.Text = name;
                    this.yearTB.Text = year;
                    this.idParentTB.Text = malerID;
                    this.galleryIDTB.Text = galleryID;

                    // this.idTB.Enabled = false; // to not set a different ID for our child element

                */

                //disable the textbox that contains the child Id so that it's not changed by accident
                foreach (TextBox textBox in textBoxList)
                {
                    if(textBox.Name == "idChildTB")
                    {
                        textBox.Enabled = false;
                    }
                }
             
            }
            updateBtn.Enabled = true;
            //update button was enabled
            deleteBtn.Enabled = true;
            //delete button was enabled
            insertBtn.Enabled = false;
            //insert button was disabled

        }

        private void updateDGV()
        {

            string parentId = dataGridView1.CurrentRow.Cells[parentPK].Value.ToString();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM " + childTableName + " WHERE " + parentPK + " = " + "'" + parentId + "'";

                adapter.SelectCommand = new SqlCommand(query, connection);

                cb = new SqlCommandBuilder(adapter);
                
                DataTable dt = new DataTable();
                adapter.Fill(dt);

                //bind the child dataGridView to the bindingSource
                //the dataSource for the second bindingSource is the bindingSource of the parent dataTable
                bindingSourceDGV2.DataSource = dt;
                //Assign to the dataGridView of the child its bindingSource 
                dataGridView2.DataSource = bindingSourceDGV2;
                //The relation is global and was defined when pressing the connect button
                //bindingSourceDGV2.DataMember = relation.RelationName;

            }
        }
        private void updateButton_Click(object sender, EventArgs e)
        {

            /*
             //create an SqlCommand to update the database tables
             updateCmd = new SqlCommand("UPDATE" + childTableName + " SET NAME=@NAME, JAHR=@JAHR, IDMALER = @IDMALER, IDGALLERY=@GALLERYID  where IDBILD = @ID", connection);
             //set the UpdateCommand of the adapter to the created SqlCommand
             adapter.UpdateCommand = updateCmd;
             //set the values to the parameters of the UpdateCommand to the input from the TextBoxes
             adapter.UpdateCommand.Parameters.Add("@NAME", SqlDbType.NChar).Value = nameTB.Text.ToString();
             adapter.UpdateCommand.Parameters.Add("@JAHR", SqlDbType.Int).Value = Convert.ToInt32(yearTB.Text);
             adapter.UpdateCommand.Parameters.Add("@IDMALER", SqlDbType.Int).Value = Convert.ToInt32(idParentTB.Text);
             adapter.UpdateCommand.Parameters.Add("@ID", SqlDbType.Int).Value = Convert.ToInt32(idChildTB.Text);
             adapter.UpdateCommand.Parameters.Add("@GALLERYID", SqlDbType.Int).Value = Convert.ToInt32(galleryIDTB.Text);

             //Connect to the database and make the changes
             connection.Open();
             adapter.UpdateCommand.ExecuteNonQuery();
             connection.Close();

            
            */


            //"UPDATE" + childTableName + " SET NAME=@NAME, JAHR=@JAHR, IDMALER = @IDMALER, IDGALLERY=@GALLERYID  where IDBILD = @ID", connection);
            
            SqlConnection connection = new SqlConnection(connectionString);
            
            Dictionary<string, string> columnList = Columns(childTableName);
            string updateQuery = "";

            string paramName = "param";

            
           
            
            int count = 0;
            for (int i = 0; i < columnList.Count ; i++)
            {
                //Add to the string only if column name is different than primary key name of the table
                //Primary key will be used at the WHERE part of the Update Query
                //for e.g. "Name=@param0, IDGallery=@param1, IDAdresse=@param2"
                if (!(columnList.ElementAt(i).Key == childPK))
                {
                    updateQuery += columnList.ElementAt(i).Key + "=@" + paramName + count;
                    count++;
                    if (i < columnList.Count - 1)
                    {
                        updateQuery += ", ";
                    }
                }
            
            }
            //Build the update command for the adapter
            //UPDATE Angestellten SET Name='Jeanne', IDGallery=1, IDAdresse=100 WHERE IDAngestellter=16
            updateCmd = new SqlCommand("UPDATE" + " " + childTableName + " SET " + updateQuery + " " + "WHERE" + " " + childPK + "=@CHILD_ID", connection);
            adapter.UpdateCommand = updateCmd;
           
            if (!textBoxInputOk()) return;

            //a counter for the parameters since the parameters have the form :param0, param1, param2 etc.
            count = 0;
            for (int i = 0; i < textBoxList.Count; i++)
            {
                //If the name of the textbox contains the name of the primary key of the child table add it as parameter to the adapter
                //the value is the text from the textBox converted to int
               
                if (textBoxList[i].Name == "idChildTB")
                {
                   
                    adapter.UpdateCommand.Parameters.AddWithValue("@CHILD_ID", Convert.ToInt32(textBoxList[i].Text));
                }

                else
                {

                    if (columnList.ElementAt(i).Value == "int")
                    {
                        int object_value = Convert.ToInt32(textBoxList[i].Text);
                        adapter.UpdateCommand.Parameters.AddWithValue("@param" + count, object_value);
                    }

                    else if(columnList.ElementAt(i).Value == "nchar")
                    {
                        string object_value = textBoxList[i].Text;
                        adapter.UpdateCommand.Parameters.AddWithValue("@param" + count, object_value);
                    }
                    //Parameter names param0, param1 etc don't include the childId parameter --> increment count only when textBox is not childId textBox
                    count++;
                }
            }

            connection.Open();
            adapter.UpdateCommand.ExecuteNonQuery();
            connection.Close();

            clearTextBoxes();
            updateDGV();

            MessageBox.Show("Database updated!", "INFO");

        }


        //Returns a dict of all the column names and as values their data_types of the table with the name "table"
        //dict : <Key: columnName,  Value: columnDataType>
        private Dictionary<string, string> Columns(string tableName)
        {
           
            using(SqlConnection connection = new SqlConnection(connectionString))
            {
                
                connection.Open();
                //Create command to get all the column names with their dataTypes of the table named tableName
                SqlCommand getColumns = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS C WHERE TABLE_NAME = @tableName", connection);
                getColumns.Parameters.AddWithValue("@tableName", tableName);
                
                //Execute the command and get the output with a SqlDataReader
                SqlDataReader reader = getColumns.ExecuteReader();
                
                //Read all the column names and add them to a list
                Dictionary<string, string> cols = new Dictionary<string, string>();
               
                while (reader.Read())
                {
                    string readerOutputName = reader.GetString(0);
                    string readerOutputDataType = reader.GetString(1);

                    cols.Add(readerOutputName, readerOutputDataType);
                }
                connection.Close();
                //Add the primary key column name again at the end of the list to maintain the order of the columns and also know which one of the the Primary Key is
               
                return cols;

            }
            
            
        } 

        private void deleteButton_Click(object sender, EventArgs e)
        {
            /*

           
           
           
           
            
           
            //Connect to the database and make the changes
            
            */

            SqlConnection connection = new SqlConnection(connectionString);

            //create an SqlCommand to delete the database tables
            deleteCmd = new SqlCommand("DELETE FROM" +" " + childTableName + " " + "WHERE" + " " + childPK + "=@CHILD_ID", connection);
            
            //set the DeleteCommand of the adapter to the created SqlCommand
            adapter.DeleteCommand = deleteCmd;
            int childId = -1;
            
            //set the parameter of the adapters DeleteCommand to the ID of the element to be deleted
            //First check if the TextBoxes have a good input
            if (!textBoxInputOk()) return;

            for ( int i = 0; i < textBoxList.Count; i++)
            {
                if (textBoxList[i].Name == "idChildTB")
                {
                    childId = Convert.ToInt32(textBoxList[i].Text);
                }
            } 
            adapter.DeleteCommand.Parameters.AddWithValue("@CHILD_ID", childId);

            connection.Open();
            adapter.DeleteCommand.ExecuteNonQuery();
            connection.Close();
            clearTextBoxes();
            
            updateDGV();

            MessageBox.Show("Database updated\n element deleted!", "INFO");

        }

        //Returns if the parameter given ID already found in the children IDs or not
         private bool alreadyInDB(string id)
        {
            DataTable dt = myDataSet.Tables[1];

            
            
            foreach( DataRow row in dt.Rows)
            {
                string cell = row[childPK].ToString();
                if ( cell == id)
                {
                    return true;
                }
            }
            return false;
        }

        private void insertButton_Click(object sender, EventArgs e)
        {
            //for e.g. "INSERT INTO Bilder VALUES(@ID, @NAME, @IDMALER, @JAHR, @GALLERYID)"

            Dictionary<string, string> columnList = Columns(childTableName);
            int index = -1;
            for (int i = 0; i < textBoxList.Count; i++)
            {
                if (textBoxList[i].Name == "idChildTB")
                {
                    index = i;
                    break;
                }
            }

            string idChildTB = textBoxList[index].Text;

            if (alreadyInDB(idChildTB))
            {
                MessageBox.Show("!ID ALREADY IN DATABASE!\nPLEASE TRY AGAIN WITH A DIFFERENT VALUE!", "INFO");
                clearTextBoxes();
                return;
            }

            string insertQuery = "INSERT INTO " + childTableName + " " + "VALUES(";



            string paramName = "param";

            int count = 0;
            for (int i = 0; i < columnList.Count; i++)
            {
                //Add to the string only if column name is different than primary key name of the table
                //Primary key will be used at the WHERE part of the Update Query
                //for e.g. "Name=@param0, IDGallery=@param1, IDAdresse=@param2"
                insertQuery += "@" + paramName + count;

                count++;
                if (i < columnList.Count - 1)
                {
                    insertQuery += ", ";
                }


            }

            insertQuery += ")";

            SqlConnection connection = new SqlConnection(connectionString);

            insertCmd = new SqlCommand(insertQuery, connection);
            adapter.InsertCommand = insertCmd;
            if (!textBoxInputOk()) return;
            count = 0;
            for (int i = 0; i < textBoxList.Count; i++)
            {

                if (columnList.ElementAt(i).Value == "int")
                {
                    int object_value = Convert.ToInt32(textBoxList[i].Text);
                    adapter.InsertCommand.Parameters.AddWithValue("@param" + count, object_value);
                }

                else if (columnList.ElementAt(i).Value == "nchar")
                {
                    string object_value = textBoxList[i].Text;
                    adapter.InsertCommand.Parameters.AddWithValue("@param" + count, object_value);
                }
                //Parameter names param0, param1 etc don't include the childId parameter --> increment count only when textBox is not childId textBox
                count++;
            }


            connection.Open();
            adapter.InsertCommand.ExecuteNonQuery();
            connection.Close();

            updateDGV();

            clearTextBoxes();
            MessageBox.Show("Database updated\n element added!", "INFO");

            /*
            

            //create an SqlCommand to delete the database tables
            //set the DeleteCommand of the adapter to the created SqlCommand
            adapter.InsertCommand = insertCmd;

            //verify the ID TextBox input for it to be made only out of digits 
            string tString = idChildTB.Text;


            //verify ID TextBox input not to be already in the childTable ID column
            foreach (DataRow row in myDataSet.Tables[1].Rows)
                if ((int)row["idBild"] == Convert.ToInt32(idChildTB.Text))
                {
                    MessageBox.Show("!ID ALREADY IN DATABASE!\nPLEASE TRY AGAIN WITH A DIFFERENT VALUE!", "INFO");
                    clearTextBoxes();
                    return;
                }

            //set the values to the parameters of the InsertCommand to the input from the TextBoxes
            adapter.InsertCommand.Parameters.Add("@NAME", SqlDbType.NChar).Value = nameTB.Text.ToString();
            adapter.InsertCommand.Parameters.Add("@JAHR", SqlDbType.Int).Value = Convert.ToInt32(yearTB.Text);
            adapter.InsertCommand.Parameters.Add("@IDMALER", SqlDbType.Int).Value = Convert.ToInt32(idParentTB.Text);
            adapter.InsertCommand.Parameters.Add("@ID", SqlDbType.Int).Value = Convert.ToInt32(idChildTB.Text);
            adapter.InsertCommand.Parameters.Add("@GALLERYID", SqlDbType.Int).Value = Convert.ToInt32(galleryIDTB.Text);

            //Connect to the database and make the changes
            connection.Open();
            adapter.InsertCommand.ExecuteNonQuery();
            connection.Close();

           
            */
        }

        private bool textBoxInputOk()
        {
            Dictionary<string, string> cols = Columns(childTableName);
            for (int i = 0; i < textBoxList.Count; i++)
            {
                if (cols.ElementAt(i).Value == "int")
                {

                    string tString = textBoxList[i].Text;

                    if (tString.Trim() == "") return false;

                    for (int j = 0; j < tString.Length; j++)
                    {
                        if (!char.IsNumber(tString[j]))
                        {
                            //MessageBox.Show("text", "title", MessageBoxButtons.OK, MessageBoxIcon.Warning); 
                            MessageBox.Show("!ONLY DIGITS ALLOWED HERE!","WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textBoxList[i].Clear();
                            return false;
                        }
                    }

                    string text = cols.ElementAt(i).Key.ToLower();
                    bool contains = text.Contains("year");
                    //if the current textBox has to have a year input
                    if (contains)
                    {
                        if (textBoxList[i].Text.Length > 4)
                        {

                           
                            MessageBox.Show("!YEAR INPUT CANNOT CONTAIN MORE THAN 4 DIGITS!", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textBoxList[i].Clear();
                            
                        }
                        else if (Convert.ToInt32(tString) > 2022)
                        {

                            MessageBox.Show("Please enter a valid year!", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textBoxList[i].Clear();
                            
                        }
                    }
                }

                else if (cols.ElementAt(i).Value == "nchar")
                {
                    string tString = textBoxList[i].Text;
                    if (tString.Trim() == "") return false;

                    for (int j = 0; j < tString.Length; j++)
                    {
                        if (char.IsNumber(tString[j]))
                        {
                            MessageBox.Show("!ONLY LETTERS ALLOWED HERE!", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textBoxList[i].Clear();
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private void createLabels()
        {


            int i = 1;
            foreach (DataColumn col in myDataSet.Tables[childTableName].Columns)
            {
                Label label = new Label();
                label.Text = col.ColumnName;
                labelList.Add(label);
                labelList[i - 1].Location = new Point(460, 25 + 43 * i);
                labelList[i - 1].Visible = true;
                Controls.Add(labelList[i - 1]);
                i += 1;

            }


        }

        private void createTextBoxes()
        {

            int i = 1;
            foreach (DataColumn col in myDataSet.Tables[childTableName].Columns)
            {
                TextBox textBox = new TextBox();
                if (col.ColumnName == childPK)
                {
                    textBox.Name = "idChildTB";
                }
                else if (col.ColumnName == childFK)
                {
                 
                    textBox.Name = "idParentTB";
                }
                else
                {
                    
                    textBox.Name = col.ColumnName + "TB";
                }

                textBoxList.Add(textBox);

                textBoxList[i - 1].Location = new Point(650, 25 + 43 * i);
                textBoxList[i - 1].Size = new Size(272, default);
                textBoxList[i - 1].Visible = true;
                Controls.Add(textBoxList[i - 1]);
                i += 1;

            }

        }
    }
}


