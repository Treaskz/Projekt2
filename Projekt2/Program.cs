using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Project> Projects { get; set; } = new List<Project>();
}
public class Project
{
    public int Id { get; set; }
    public string Name { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
}
public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Project> Projects { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> GetByIdAsync(int id);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}
public class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.ToListAsync();

    public async Task<T> GetByIdAsync(int id) =>
        await _dbSet.FindAsync(id);

    public async Task AddAsync(T entity)
    {
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
public interface IProjectService
{
    Task<IEnumerable<Project>> GetAllProjectsAsync();
    Task<Project> GetProjectByIdAsync(int id);
    Task CreateProjectAsync(string projectName, string customerName);
    Task UpdateProjectAsync(int id, string newProjectName);
    Task DeleteProjectAsync(int id);
}

public interface ICustomerService
{
    Task<IEnumerable<Customer>> GetAllCustomersAsync();
    Task<Customer> GetCustomerByNameAsync(string name);
    Task<Customer> CreateCustomerAsync(string name);
}

public class ProjectService : IProjectService
{
    private readonly IRepository<Project> _projectRepository;
    private readonly ICustomerService _customerService;

    public ProjectService(IRepository<Project> projectRepository, ICustomerService customerService)
    {
        _projectRepository = projectRepository;
        _customerService = customerService;
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync() =>
        await _projectRepository.GetAllAsync();

    public async Task<Project> GetProjectByIdAsync(int id) =>
        await _projectRepository.GetByIdAsync(id);

    public async Task CreateProjectAsync(string projectName, string customerName)
    {
        var customer = await _customerService.GetCustomerByNameAsync(customerName)
                       ?? await _customerService.CreateCustomerAsync(customerName);

        var project = new Project
        {
            Name = projectName,
            CustomerId = customer.Id,
            Customer = customer
        };
        await _projectRepository.AddAsync(project);
    }

    public async Task UpdateProjectAsync(int id, string newProjectName)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project != null)
        {
            project.Name = newProjectName;
            await _projectRepository.UpdateAsync(project);
        }
    }

    public async Task DeleteProjectAsync(int id) =>
        await _projectRepository.DeleteAsync(id);
}

public class CustomerService : ICustomerService
{
    private readonly IRepository<Customer> _customerRepository;

    public CustomerService(IRepository<Customer> customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<IEnumerable<Customer>> GetAllCustomersAsync() =>
        await _customerRepository.GetAllAsync();

    public async Task<Customer> GetCustomerByNameAsync(string name)
    {
        var customers = await _customerRepository.GetAllAsync();
        foreach (var customer in customers)
        {
            if (customer.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return customer;
        }
        return null;
    }

    public async Task<Customer> CreateCustomerAsync(string name)
    {
        var customer = new Customer { Name = name };
        await _customerRepository.AddAsync(customer);
        return customer;
    }
}
class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MyDatabase;Trusted_Connection=True;", sqlOptions => sqlOptions.EnableRetryOnFailure()));

                services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
                services.AddScoped<ICustomerService, CustomerService>();
                services.AddScoped<IProjectService, ProjectService>();
            })
            .Build();

        var projectService = host.Services.GetRequiredService<IProjectService>();

        bool exit = false;
        while (!exit)
        {
            Console.WriteLine("Välj alternativ:");
            Console.WriteLine("1. Lista alla projekt");
            Console.WriteLine("2. Skapa nytt projekt");
            Console.WriteLine("3. Editera/uppdatera projekt");
            Console.WriteLine("4. Avsluta");
            Console.Write("Ditt val: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    var projects = await projectService.GetAllProjectsAsync();
                    Console.WriteLine("Projektlista:");
                    foreach (var proj in projects)
                    {
                        string newCustomerName = proj.Customer != null ? proj.Customer.Name : "Okänd";
                        Console.WriteLine($"ID: {proj.Id}, Namn: {proj.Name}, Kund: {newCustomerName}");
                    }
                    break;

                case "2":
                    Console.Write("Ange projektnamn: ");
                    string projectName = Console.ReadLine();
                    Console.Write("Ange kundnamn: ");
                    string customerName = Console.ReadLine();
                    await projectService.CreateProjectAsync(projectName, customerName);
                    Console.WriteLine("Projekt skapat.");
                    break;

                case "3":
                    Console.Write("Ange projektets ID att uppdatera: ");
                    if (int.TryParse(Console.ReadLine(), out int id))
                    {
                        Console.Write("Ange nytt projektnamn: ");
                        string newName = Console.ReadLine();
                        await projectService.UpdateProjectAsync(id, newName);
                        Console.WriteLine("Projekt uppdaterat.");
                    }
                    else
                    {
                        Console.WriteLine("Felaktigt ID.");
                    }
                    break;

                case "4":
                    exit = true;
                    break;

                default:
                    Console.WriteLine("Ogiltigt val.");
                    break;
            }
            Console.WriteLine();
        }
    }
}
