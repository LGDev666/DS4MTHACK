#include <windows.h>
#include <iostream>
#include <string>

// Definições de tipos para as funções da DLL
typedef int (*WinHelperInit)();
typedef void* (*HelperCreateDevice)(unsigned short, unsigned short, unsigned short, unsigned short, unsigned short, const char*, const char*);
typedef bool (*HelperDestroyDevice)(void*);

// Função para apagar o cabeçalho PE
void ErasePEHeader(HMODULE module) {
    DWORD oldProtect;
    VirtualProtect(module, 4096, PAGE_EXECUTE_READWRITE, &oldProtect);
    ZeroMemory(module, 4096);
    VirtualProtect(module, 4096, oldProtect, &oldProtect);
}

// Função para verificar se há um debugger anexado
BOOL IsDebuggerAttached() {
    BOOL remoteDebugger = FALSE;
    CheckRemoteDebuggerPresent(GetCurrentProcess(), &remoteDebugger);
    return IsDebuggerPresent() || remoteDebugger;
}

int main() {
    // Verificar se há um debugger anexado
    if (IsDebuggerAttached()) {
        return -1;
    }

    // Apagar o cabeçalho PE do executável atual
    ErasePEHeader(GetModuleHandle(NULL));

    // Carregar a DLL obfuscada
    HMODULE hMod = LoadLibraryW(L"wininput_helper64.dll");
    if (!hMod) {
        std::cerr << "Falha ao carregar a DLL: " << GetLastError() << std::endl;
        return -1;
    }

    // Obter os endereços das funções
    WinHelperInit init = (WinHelperInit)GetProcAddress(hMod, "WinHelper_Init");
    if (!init) {
        std::cerr << "Falha ao obter o endereço da função de inicialização: " << GetLastError() << std::endl;
        FreeLibrary(hMod);
        return -1;
    }

    // Inicializar a DLL
    int result = init();
    if (result != 0) {
        std::cerr << "Falha ao inicializar a DLL: " << result << std::endl;
        FreeLibrary(hMod);
        return -1;
    }

    std::cout << "DLL carregada e inicializada com sucesso!" << std::endl;
    std::cout << "Pressione Enter para sair..." << std::endl;
    std::cin.get();

    // Liberar a DLL
    FreeLibrary(hMod);
    return 0;
}