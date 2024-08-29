using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

public class AuthApiServer : MonoBehaviour
{
    private HttpListener _listener;
    private readonly Dictionary<string, string> _userStore = new Dictionary<string, string>();
    private string _filePath;

    private void Awake()
    {
        // Initialize the file path in Awake to ensure it's set before Start is called
        _filePath = Path.Combine(Application.persistentDataPath, "userData.json");
    }

    private void Start()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:8080/");
        _listener.Start();
        Debug.Log("API Server started on http://localhost:8080/");

        LoadDataFromJson();
        Listen();
    }

    private void Listen()
    {
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            while (true)
            {
                var context = _listener.GetContext();
                var request = context.Request;
                var response = context.Response;

                string responseString = "";
                try
                {
                    if (request.HttpMethod == "POST")
                    {
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            var postData = reader.ReadToEnd();
                            var data = ParsePostData(postData);

                            if (request.Url.AbsolutePath.Contains("/signup"))
                            {
                                responseString = HandleSignUp(data);
                            }
                            else if (request.Url.AbsolutePath.Contains("/login"))
                            {
                                responseString = HandleLogin(data);
                            }
                            else
                            {
                                responseString = "{\"error\": \"Invalid endpoint\"}";
                            }
                        }
                    }
                    else if (request.HttpMethod == "GET")
                    {
                        var queryParams = ParseQueryString(request.Url.Query);
                        if (request.Url.AbsolutePath.Contains("/user"))
                        {
                            responseString = HandleGetUser(queryParams);
                        }
                        else
                        {
                            responseString = "{\"error\": \"Invalid endpoint\"}";
                        }
                    }
                    else
                    {
                        responseString = "{\"error\": \"Only POST and GET requests are supported\"}";
                    }
                }
                catch (Exception ex)
                {
                    responseString = "{\"error\": \"" + ex.Message + "\"}";
                }

                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;

                using (var output = response.OutputStream)
                {
                    output.Write(buffer, 0, buffer.Length);
                }
            }
        });
    }

    private string HandleSignUp(Dictionary<string, string> data)
    {
        if (!data.ContainsKey("username") || !data.ContainsKey("password"))
            return "{\"error\": \"Username and password are required\"}";

        var username = data["username"];
        var password = data["password"];

        if (_userStore.ContainsKey(username))
            return "{\"error\": \"User already exists\"}";

        _userStore[username] = password;
        SaveDataToJson();
        return "{\"message\": \"User registered successfully\"}";
    }

    private string HandleLogin(Dictionary<string, string> data)
    {
        if (!data.ContainsKey("username") || !data.ContainsKey("password"))
            return "{\"error\": \"Username and password are required\"}";

        var username = data["username"];
        var password = data["password"];

        if (_userStore.TryGetValue(username, out var storedPassword))
        {
            if (storedPassword == password)
                return "{\"message\": \"Login successful\"}";

            return "{\"error\": \"Invalid password\"}";
        }
        return "{\"error\": \"User does not exist\"}";
    }

    private string HandleGetUser(Dictionary<string, string> queryParams)
    {
        if (!queryParams.ContainsKey("username"))
            return "{\"error\": \"Username is required\"}";

        var username = queryParams["username"];

        if (_userStore.TryGetValue(username, out var password))
        {
            return JsonUtility.ToJson(new User { username = username, password = password });
        }
        return "{\"error\": \"User does not exist\"}";
    }

    private Dictionary<string, string> ParsePostData(string postData)
    {
        var data = new Dictionary<string, string>();
        var keyValuePairs = postData.Split('&');
        foreach (var pair in keyValuePairs)
        {
            var keyValue = pair.Split('=');
            if (keyValue.Length == 2)
            {
                data[Uri.UnescapeDataString(keyValue[0])] = Uri.UnescapeDataString(keyValue[1]);
            }
        }
        return data;
    }

    private Dictionary<string, string> ParseQueryString(string queryString)
    {
        var data = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(queryString))
        {
            var keyValuePairs = queryString.TrimStart('?').Split('&');
            foreach (var pair in keyValuePairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    data[Uri.UnescapeDataString(keyValue[0])] = Uri.UnescapeDataString(keyValue[1]);
                }
            }
        }
        return data;
    }

    private void SaveDataToJson()
    {
        var userList = new List<User>();
        foreach (var kvp in _userStore)
        {
            userList.Add(new User { username = kvp.Key, password = kvp.Value });
        }

        var userStore = new UserStore { users = userList };
        var json = JsonUtility.ToJson(userStore, true);
        File.WriteAllText(_filePath, json);
        Debug.Log($"Data saved to {_filePath}");
    }

    private void LoadDataFromJson()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var userStore = JsonUtility.FromJson<UserStore>(json);
            _userStore.Clear();
            foreach (var user in userStore.users)
            {
                _userStore[user.username] = user.password;
            }
            Debug.Log($"Data loaded from {_filePath}");
        }
    }

    private void OnApplicationQuit()
    {
        SaveDataToJson();
        _listener.Stop();
    }

    [Serializable]
    private class User
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class UserStore
    {
        public List<User> users;
    }
}
