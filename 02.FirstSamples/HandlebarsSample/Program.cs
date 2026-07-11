using Fluid;
using HandlebarsDotNet;

var template = @"""
Hello, {{customer.firstName}}! Your membership is: {{customer.membership}}.
{{#each history}}
  {{role}}: {{content}}
{{/each}}
""";

// Compile the template
var handlebars = Handlebars.Create();
var compiled = handlebars.Compile(template);

// Prepare data context
var data = new
{
  customer = new { firstName = "John", membership = "Gold" },
  history = new[] {
    new { role = "user", content = "What is my membership level?" }
  }
};

// Render template
Console.WriteLine(compiled(data));


template = """
Hello, {{ customer.firstName }}! Your membership is: {{ customer.membership }}.
{% for item in history %}
  {{ item.role }}: {{ item.content }}
{% endfor %}
""";

// Parse and render the template
var parser = new FluidParser();
parser.TryParse(template, out var fluidTemplate);

var options = new TemplateOptions
{
  MemberAccessStrategy = new DefaultMemberAccessStrategy(),
};

var context = new TemplateContext(options);
context.SetValue("customer", new { firstName = "John", membership = "Gold" });
context.SetValue("history", new[] {
  new { role = "user", content = "What is my membership level?" }
});

Console.WriteLine(fluidTemplate.Render(context));
