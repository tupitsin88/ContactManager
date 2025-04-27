// Тупицин Тимофей Романович БПИ2410-1 Вариант 6 B-side

using System.Text;
using Library.Menu;

namespace ConsoleApp
{
    public class Program
    {
        private static async Task Main()
        {
            // Устанавливаем кодировку консоли.
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Запускаем основную логику программы.
            await InitializeAndRun();
        }

        /// <summary>
        /// Инициализирует и запускает основную логику программы.
        /// </summary>
        /// <remarks>
        /// Метод запрашивает путь к файлу с контактами, проверяет его существование,
        /// считывает данные из файла и передает управление в главное меню.
        /// </remarks>
        private static async Task InitializeAndRun()
        {
            // Запрашиваем путь к файлу с контактами.
            Console.Write("Введите путь к файлу с контактами: ");
            string filePath = Console.ReadLine()!;

            // Проверяем существование файла.
            while (!File.Exists(filePath))
            {
                Console.Write("Файл не существует. Попробуйте снова: ");
                filePath = Console.ReadLine()!;
            }

            // Считываем данные из файла.
            string input = File.ReadAllText(filePath);

            // Запускаем главное меню.
            await Menu.ShowMainMenu(input, filePath);
        }
    }
}
