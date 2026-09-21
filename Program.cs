using Google.GenAI;

var client = new Client();

var response = await client.Models.GenerateContentAsync(
    model:"gemini-3.8-flash",
    contents: Console.ReadLine()
);

Console.WriteLine(response.Candidates[0].Content.Parts[0].Text);