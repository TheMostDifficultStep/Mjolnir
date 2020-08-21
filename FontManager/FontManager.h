// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the FONTMANAGER_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// FONTMANAGER_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef FONTMANAGER_EXPORTS
#define FONTMANAGER_API __declspec(dllexport)
#else
#define FONTMANAGER_API __declspec(dllimport)
#endif

// This class is exported from the dll
class FONTMANAGER_API CFontManager {
public:
	CFontManager(void);
	// TODO: add your methods here.
};

extern FONTMANAGER_API int nFontManager;

FONTMANAGER_API int fnFontManager(void);
