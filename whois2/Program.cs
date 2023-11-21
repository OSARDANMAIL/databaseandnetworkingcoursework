
using Microsoft.VisualBasic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using MySql.Data;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using System.Runtime.CompilerServices;
using System.ComponentModel.Design;

public class Mainclass
{

    static Boolean debug = true;

    static string connStr = "server=localhost; user=root; database=users ;port=3306;password=L3tM31n";

    public static void Main(string[] args)
    {

        if (args.Length == 0)
        {
            Console.WriteLine("Starting Server");
            RunServer();
        }
        else
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    Console.WriteLine("Connecting to MySQL--- world database");
                    conn.Open();
                    // Perform database operations
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                //conn.Close(); // close the connection
                Console.WriteLine("Done the job");
                for (int i = 0; i < args.Length; i++)
                {
                    ProcessCommand(conn, args[i]);
                }
            }

        }
    }

    //static void doRequest(NetworkStream socketStream, MySqlConnection connection)
    //{
    //    StreamWriter sw = new StreamWriter(socketStream);
    //    StreamReader sr = new StreamReader(socketStream);
    //    if (debug) Console.WriteLine("Waiting for input from client...");

    //    try
    //    {
    //        String line = sr.ReadLine();

    //        if (line == null || line.StartsWith("GET /favicon.ico"))
    //        {
    //            if (debug) Console.WriteLine("Ignoring irrelevant command");
    //            return;
    //        }
    //        Console.WriteLine($"Received Network Command: '{line}'");

    //        if (line.StartsWith("GET /?name=") && line.EndsWith(" HTTP/1.1"))
    //        {
    //            String username = line.Substring(10, line.IndexOf(" HTTP/1.1") - 10);
    //            String response = ProcessGetRequest(connection, username);

    //            sw.WriteLine("HTTP/1.1 200 OK");
    //            sw.WriteLine("Content-Type: text/plain");
    //            sw.WriteLine();
    //            sw.WriteLine(response);
    //            sw.Flush();
    //        }
    //        else
    //        {
    //            sw.WriteLine("HTTP/1.1 400 Bad Request");
    //            sw.WriteLine("Content-Type: text/plain");
    //            sw.WriteLine();
    //            sw.Flush();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Error in request processing: {ex.Message}");
    //        sw.WriteLine("HTTP/1.1 500 Internal Server Error");
    //        sw.WriteLine("Content-Type: text/plain");
    //        sw.WriteLine();
    //        sw.Flush();
    //    }
    //    finally
    //    {
    //        sw.Close();
    //        sr.Close();
    //    }
    //}

    static void  doRequest(NetworkStream socketStream, MySqlConnection connection)
    {
        StreamWriter sw = new StreamWriter(socketStream);
        StreamReader sr = new StreamReader(socketStream);
        if (debug) Console.WriteLine("Waiting for input from client...");
        String line = sr.ReadLine();
        Console.WriteLine($"Received Network Command: '{line}'");

        if (line == "POST / HTTP/1.1")
        {
            // The we have an update
            if (debug) Console.WriteLine("Received an update request");
            // Add your POST request handling logic here
        }
        else if (line.StartsWith("GET /?name=") && line.EndsWith(" HTTP/1.1"))
        {
            // then we have a lookup
            if (debug) Console.WriteLine("Received a lookup request");

            String[] slices = line.Split(" ");  // Split into 3 pieces
            String ID = slices[1].Substring(7);  // start at the 7th letter of the middle slice - skip `/?name=`
            String result = ProcessGetRequest(connection, ID);

            if (!string.IsNullOrEmpty(result))
            {
                sw.WriteLine("HTTP/1.1 200 OK");
                sw.WriteLine("Content-Type: text/plain");
                sw.WriteLine();
                sw.WriteLine(result);
                sw.Flush();
                Console.WriteLine($"Performed Lookup on '{ID}' returning '{result}'");
            }
            else
            {
                sw.WriteLine("HTTP/1.1 404 Not Found");
                sw.WriteLine("Content-Type: text/plain");
                sw.WriteLine();
                sw.Flush();
                Console.WriteLine($"Performed Lookup on '{ID}' returning '404 Not Found'");
            }
        }
        else
        {
            // We have an error
            Console.WriteLine($"Unrecognised command: '{line}'");
            sw.WriteLine("HTTP/1.1 400 Bad Request");
            sw.WriteLine("Content-Type: text/plain");
            sw.WriteLine();
            sw.Flush();
        }
    }



    static String ProcessGetRequest(MySqlConnection connection, String username)
    {
        try
        {
            String query = @"
            SELECT uloc.Location
            FROM user_login ul
            INNER JOIN users_location uloc ON ul.UserID = uloc.UserID
            WHERE ul.LoginID = @username;
        ";

            using (MySqlCommand cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@username", username);
                object result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    return result.ToString();
                }
                else
                {
                    return "Location not found for username: " + username;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ProcessGetRequest: {ex.Message}");
            return "Error retrieving location.";
        }
    }




    static void RunServer()
    {
        TcpListener listener;
        Socket connection;
        NetworkStream socketStream;

        using (MySqlConnection conn = new MySqlConnection(connStr))
        {

            try
            {

                conn.Open();
                listener = new TcpListener(43);
                listener.Start();

                bool isRunning = true;

                while (true)
                {
                    if (debug) Console.WriteLine("Server Waiting connection...");
                    connection = listener.AcceptSocket();
                    connection.SendTimeout = 1000;
                    connection.ReceiveTimeout = 1000;
                    socketStream = new NetworkStream(connection);
                    doRequest(socketStream, conn);
                    socketStream.Close();
                    connection.Close();
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            if (debug)
                Console.WriteLine("Terminating Server");

        }


    }
    /// Process the next database command request
    static void ProcessCommand(MySqlConnection connection, string command)
    {
        Console.WriteLine($"\nCommand: {command}");
        try
        {
            String[] slice = command.Split(new char[] { '?' }, 2);
            String ID = slice[0];
            String operation = null;
            String update = null;
            String field = null;


            if (slice.Length == 2)
            {
                operation = slice[1];
                String[] pieces = operation.Split(new char[] { '=' }, 2);
                field = pieces[0];
                if (pieces.Length == 2) update = pieces[1];
            }

            if (operation == null) Dump(connection, ID);
            else if (update == null) Lookup(connection, ID, field);
            else Update(connection, ID, field, update);

            if (slice.Length == 2)
            {
                operation = slice[1];
                if (string.IsNullOrEmpty(operation.Trim('?')))
                {
                    Delete(connection, ID);
                    return;
                }

            }
        }
        catch (Exception ex) 
        {

            Console.WriteLine($"Fault in Command Processing: {ex.ToString()}"); 
        
        
        }
      
        
    }


    /// Functions to process database requests
    static void Delete(MySqlConnection connection ,String ID)
    {
        if (debug) Console.WriteLine($"Delete record '{ID}' from DataBase");
        //DataBase.Remove(ID);

        string sqlcommand = $@"SET SQL_SAFE_UPDATES = 0;

          DELETE FROM user_login WHERE loginID = ""{ID}"" ;";


        DoQuery(connection, sqlcommand); 
    }
    /// Functions to process database requests
    static void Dump(MySqlConnection connection, String ID)
    {
        if (ifIDEXIST(connection, ID))
        {

          Console.WriteLine($"User '{ID}' can't be found");
          return;
        }


        if (debug) Console.WriteLine(" output all fields");

        String sqlcommand = $@"  SELECT
    up.UserID,
    up.Surname,
    up.Forenames,
    up.Title,
    up.Position,
    uph.Phone,
    ed.Email,
    uloc.Location
FROM
    emaildetails ed
JOIN user_login ul ON ed.UserID = ul.UserID
JOIN user_names un ON ed.UserID = un.UserID
JOIN userinfo up ON ed.UserID = up.UserID
JOIN user_phone uph ON ed.UserID = uph.UserID
JOIN users_location uloc ON ed.UserID = uloc.UserID
WHERE
    ul.LoginID = '{ID}';
";

        if (debug)
        {
            Console.WriteLine($"Sql Query for all field: {sqlcommand}"); 
        }
        DoQuery(connection, sqlcommand);

    }
    static void Lookup(MySqlConnection connection, string ID, string field)
    {

        if (string.IsNullOrWhiteSpace(field))
        {
            Console.WriteLine($"Delete record '{ID}' from database"); 
            Delete(connection, ID);
            return;
        }
        

        string sqlcommand = $@"SELECT uloc.Location
                            FROM user_login ul
                            JOIN users_location uloc ON ul.UserID = uloc.UserID
                            WHERE ul.LoginID = '{ID}'";

        if (debug) { Console.WriteLine($"Executing SQL Query: {sqlcommand}"); }
        DoQuery4lookup(connection, sqlcommand, field);
    }

    static void Update(MySqlConnection connection, String ID, String field, String update)
    {
        if (debug) Console.WriteLine($" update field '{field}' to '{update}'");

        if (!ifIDEXIST(connection, ID))
        {
            insertnewdetails (connection, ID);
        }

        string sqlcommand = $@"UPDATE users_location
                            SET Location = '{update}'
                            WHERE UserID IN (SELECT UserID FROM user_login WHERE LoginID = '{ID}');";

        DoUpdate(connection, sqlcommand);

        Console.WriteLine("OK");
    }

    static bool ifIDEXIST (MySqlConnection connection, string ID)
    {
        string sqlcommand = $@"SELECT COUNT(*) FROM user_login WHERE LoginID = '{ID}';";
        using (MySqlCommand cmd = new MySqlCommand( sqlcommand, connection ))
        {
            int countnumberofid = Convert.ToInt32 ( cmd.ExecuteScalar() );
            return countnumberofid > 0; 

        }
    }


    static void DoQuery(MySqlConnection connection, String sqlcommand)
    {
        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
        {
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    for (int i = 0; i < rdr.FieldCount; i++)
                    {

                        Console.WriteLine(rdr[0] + " -- " + rdr[1]);
                    }

                }
                if (!rdr.HasRows)
                {
                    Console.WriteLine("result cannot be detected");
                    return; 
                }

            }


        }

    }

    static void DoQuery4lookup(MySqlConnection connection, String sqlcommand, string field)
    {

        try
        {

            using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
            {
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (!rdr.HasRows)
                    {
                        Console.WriteLine("Result not Found. ");
                        return;


                    }
                    while (rdr.Read())
                    {
                        int indexcolumn;
                        try
                        {
                            indexcolumn = rdr.GetOrdinal(field);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.WriteLine($"Field '{field}' can't be found in the result set. ");
                            return;
                        }
                        // Check if the specified field exists in the result set
                        if (rdr[indexcolumn] != DBNull.Value)
                        {
                            Console.WriteLine(rdr[field]);
                        }
                        else
                        {
                            Console.WriteLine($"Field '{field}' is DBNull in the result set.");
                        }
                    }


                }
            }
        }
        catch (Exception ex) 
        { 
        
        
           Console.WriteLine($"Error in Doqueryforlookup: { ex.ToString()}");
        
        }
        
        
        
        
    }
    static string PerformLookup(MySqlConnection connection, String ID, String field)
    {
        string query = $@"SELECT location FROM Location WHERE Login_id = '{ID}';";

        // Debug output
        Console.WriteLine($"Lookup Query: {query}");

        using (MySqlCommand cmd = new MySqlCommand(query, connection))
        {
            object result = cmd.ExecuteScalar();

            // Debug output
            Console.WriteLine($"Lookup Result: {result}");

            return result?.ToString() ?? string.Empty;
        }
    }
    


    static void DoUpdate(MySqlConnection connection, String sqlcommand)
    {
        Console.WriteLine($"Executing update query: {sqlcommand}");

        using (MySqlCommand cmd = new MySqlCommand(sqlcommand, connection))
        {
            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                Console.WriteLine($"Update successful. {rowsAffected} row(s) affected.");
            }
            else
            {
                Console.WriteLine("Update did not affect any rows.");
            }
        }
    }

    static void insertnewdetails(MySqlConnection connection, string ID)
    {
        try
        {

            String sqlQuery = $@"INSERT INTO user_login (LoginID, UserID) VALUES ('{ID}', '{ID}');";

            String sqlQuery2 = $@"INSERT INTO users_location (Location, UserID) VALUES ('', '{ID}');";
          
            using (MySqlTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(sqlQuery, connection, transaction))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    using (MySqlCommand cmd = new MySqlCommand(sqlQuery2, connection, transaction))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    transaction.Commit();


                }
                catch (Exception ex)
                {

                    transaction.Rollback();
                    Console.WriteLine($"Error in insertnewdetails: {ex.ToString()}");

                    Console.WriteLine(sqlQuery.ToString());
                    Console.WriteLine(sqlQuery2.ToString());
                }


            }



        }
        catch (Exception ex)
        {
            Console.WriteLine($"errot in insertnewdetails: {ex.ToString()}");
        }

        
        

        


    }



}

