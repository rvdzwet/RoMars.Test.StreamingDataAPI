# Contributing to RoMars.Test.StreamingDataAPI

Thank you for your interest in contributing to this project! We welcome contributions from everyone.

To ensure a smooth and effective collaboration, please follow these guidelines.

## How to Contribute

1.  **Fork the Repository:** Start by forking the [RoMars.Test.StreamingDataAPI](https://github.com/yourusername/RoMars.Test.StreamingDataAPI) repository to your GitHub account.

2.  **Clone Your Fork:** Clone your forked repository to your local machine:
    ```bash
    git clone https://github.com/your-username/RoMars.Test.StreamingDataAPI.git
    cd RoMars.Test.StreamingDataAPI
    ```

3.  **Create a New Branch:** Create a new branch for your feature or bug fix. Use a descriptive name:
    ```bash
    git checkout -b feature/your-feature-name 
    # or
    git checkout -b bugfix/issue-description
    ```

4.  **Make Your Changes:** Implement your changes, keeping in mind the project's goals of high performance, SOLID principles, and clean coding standards.

    *   **Adhere to SOLID Principles:** Ensure your changes maintain or improve adherence to Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, and Dependency Inversion Principles.
    *   **Clean Code Standards:** Follow consistent naming conventions, maintain readability, and write self-documenting code.
    *   **Performance Comments:** If your changes introduce or affect performance-critical sections, add comments similar to existing ones explaining the optimization rationale. For changes affecting wide tables (105 columns) or streaming, detail how performance is maintained or improved.
    *   **Logging:** Ensure appropriate trace-level logging is maintained or added for performance-critical operations.

5.  **Test Your Changes:** Before submitting, thoroughly test your changes to ensure they work as expected and do not introduce regressions.

6.  **Commit Your Changes:** Write clear and concise commit messages. A good commit message describes *what* changed and *why*.
    ```bash
    git commit -m "feat: Add new feature"
    # or
    git commit -m "fix: Resolve bug in data streaming"
    ```

7.  **Push to Your Fork:** Push your local branch to your forked repository on GitHub:
    ```bash
    git push origin feature/your-feature-name
    ```

8.  **Create a Pull Request (PR):**
    *   Go to the original [RoMars.Test.StreamingDataAPI](https://github.com/yourusername/RoMars.Test.StreamingDataAPI) repository on GitHub.
    *   You should see a prompt to create a new pull request from your recently pushed branch.
    *   Provide a detailed description of your changes in the pull request. Explain the problem your PR solves, how it solves it, and any new features or improvements it introduces.
    *   Reference any relevant issues (e.g., `Closes #123`).

## Code Style

*   We generally follow the [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) recommended by Microsoft, along with a focus on code readability, maintainability, and performance for streaming wide document metadata.
*   Use meaningful names for variables, methods, and classes, especially for the 105 document metadata columns.
*   Keep methods concise and focused on a single responsibility.
*   Add comments where the code's intent is not immediately obvious, especially for complex logic or performance-critical sections (e.g., handling wide tables during streaming).

## Issue Reporting

If you find a bug or have a feature request, please open an issue on the [GitHub Issues page](https://github.com/yourusername/RoMars.Test.StreamingDataAPI/issues).

When reporting a bug, please include:
*   A clear and concise description of the bug.
*   Steps to reproduce the behavior.
*   Expected behavior.
*   Screenshots or error messages, if applicable.
*   Your environment details (OS, .NET SDK version, SQL Server version).

## License

By contributing to RoMars.Test.StreamingDataAPI, you agree that your contributions will be licensed under the MIT License.

We look forward to your contributions!
