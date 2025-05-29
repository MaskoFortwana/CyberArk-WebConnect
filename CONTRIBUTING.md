# Contributing to WebConnect

We welcome contributions to the WebConnect project! This document provides guidelines for contributing to the project.

## Code of Conduct

By participating in this project, you agree to abide by our code of conduct. Please be respectful and constructive in all interactions.

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Create a new branch for your feature or bug fix
4. Make your changes
5. Test your changes thoroughly
6. Submit a pull request

## Development Setup

1. Ensure you have the required dependencies:
   - .NET 8.0 SDK or later
   - PowerShell 5.1 or later
   - Google Chrome (for testing)

2. Clone the repository:
   ```bash
   git clone https://github.com/MaskoFortwana/webconnect.git
   cd webconnect
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

## Making Changes

### Branch Naming
- Feature branches: `feature/description-of-feature`
- Bug fixes: `bugfix/description-of-bug`
- Documentation: `docs/description-of-change`

### Commit Messages
- Use clear, descriptive commit messages
- Start with a verb in the imperative mood (e.g., "Add", "Fix", "Update")
- Include relevant issue numbers when applicable

### Code Style
- Follow the existing code style and conventions
- Use meaningful variable and method names
- Include appropriate comments for complex logic
- Ensure code is properly formatted

### Testing
- Add unit tests for new functionality
- Ensure all existing tests pass
- Update integration tests when necessary
- Test your changes on Windows environments

## Documentation

- Update relevant documentation when making changes
- Include examples for new features
- Update the README.md if your changes affect setup or usage
- Follow the existing documentation style and format

## Pull Request Process

1. Ensure your code builds and all tests pass
2. Update documentation as needed
3. Fill out the pull request template completely
4. Link any relevant issues
5. Request review from maintainers

### Pull Request Requirements
- [ ] Code builds successfully
- [ ] All tests pass
- [ ] Documentation updated
- [ ] No merge conflicts
- [ ] Descriptive title and description

## Reporting Issues

When reporting issues, please include:
- WebConnect version
- Operating System and version
- Steps to reproduce the issue
- Expected behavior
- Actual behavior
- Any error messages or logs

## Questions and Support

- Check the [FAQ](docs/faq.md) for common questions
- Review existing [issues](https://github.com/MaskoFortwana/webconnect/issues)
- Search [discussions](https://github.com/MaskoFortwana/webconnect/discussions)
- Create a new issue for bug reports or feature requests

## License

By contributing to WebConnect, you agree that your contributions will be licensed under the same license as the project (MIT License).

## Recognition

Contributors will be recognized in the project documentation and release notes.

Thank you for contributing to WebConnect! 