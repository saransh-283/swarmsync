var client = new HttpClient();

var result =
    await client.GetStringAsync(
        "http://localhost:5136");

Console.WriteLine(result);
