// FontManager.cpp : Defines the exported functions for the DLL.
//

#include "framework.h"
#include "FontManager.h"

#include <iostream>
#include <ft2build.h>
#include <ftbitmap.h>
#include <Windows.h>
#include <WINGDI.H>
#include <stdio.h>

#include FT_FREETYPE_H
#include FT_BITMAP_H

// This is an example of an exported variable
FONTMANAGER_API int nFontManager=0;

// This is an example of an exported function.
FONTMANAGER_API int fnFontManager(void)
{
    return 0;
}

// This is the constructor of a class that has been exported.
CFontManager::CFontManager()
{
    return;
}

extern "C" {

    struct FTGlyphPos {
        FT_Short left;
        FT_Short top;
        FT_Short advance_x;
        FT_Short advance_y;
        FT_Short delta_lsb;
        FT_Short delta_rsb;
    };

    struct FTGlyphBmp {
        unsigned short rows;
        unsigned short width;
        short          pitch;
        unsigned short num_grays;
        unsigned char  pixel_mode;
        unsigned char* bits;
    };

    /// <summary>
    /// Save the bitmap to a file.
    /// </summary>
    FONTMANAGER_API bool PG_Save(FT_Bitmap oBmp, const char* strFile) {
        BITMAPFILEHEADER sBFH;
        BITMAPINFOHEADER sBIH;
        DWORD dwColorTable = 256;

        const int iPadding = 4 - oBmp.pitch % 4;


        sBIH.biSize = sizeof(BITMAPINFOHEADER);
        sBIH.biWidth = oBmp.width;
        sBIH.biHeight = oBmp.rows;
        sBIH.biPlanes = 1;
        sBIH.biBitCount = 8;
        sBIH.biCompression = 0;
        sBIH.biSizeImage = (oBmp.pitch + iPadding) * oBmp.rows;
        sBIH.biXPelsPerMeter = 0;
        sBIH.biYPelsPerMeter = 0;
        sBIH.biClrUsed = 0;
        sBIH.biClrImportant = 0;

        DWORD dwTop = sizeof(BITMAPFILEHEADER) + sBIH.biSize + (dwColorTable * sizeof(DWORD));

        sBFH.bfType = 0x4d42;
        sBFH.bfSize = dwTop + sBIH.biSizeImage;
        sBFH.bfOffBits = dwTop;
        sBFH.bfReserved1 = 0;
        sBFH.bfReserved2 = 0;

        FILE* pFile;
        errno_t iError = fopen_s(&pFile, strFile, "wb");

        if (iError != 0)
            return false;

        fwrite(&sBFH, sizeof(sBFH), 1, pFile);
        fwrite(&sBIH, sizeof(sBIH), 1, pFile);

        const BYTE btZero = 0;
        for (int i = 0; i < (int)dwColorTable; ++i) {
            BYTE btColor = i;
            fwrite(&btColor, 1, 1, pFile);
            fwrite(&btColor, 1, 1, pFile);
            fwrite(&btColor, 1, 1, pFile);
            fwrite(&btZero, 1, 1, pFile);
        }

        if (oBmp.pitch > 0) {
            // The origin of a FT_Bitmap is lower left for positive pitch. So we flip it
            // over to match BMP origin of upper left.
            for (int iRow = oBmp.rows - 1; iRow >= 0; --iRow) {
                fwrite(oBmp.buffer + (iRow * oBmp.pitch), oBmp.pitch, 1, pFile);

                // BMP needs to be dword aligned, so we need to add the given padding per row.
                for (int iPad = 0; iPad < iPadding; ++iPad)
                    fwrite(&btZero, 1, 1, pFile);
            }
        }

        fclose(pFile);
        return true;
    }

    FONTMANAGER_API int PG_FreeType_Init(FT_Library* pLib) 
    {
        return FT_Init_FreeType((FT_Library*)pLib);
    }

    FONTMANAGER_API int PG_FreeType_Done(FT_Library hLibrary) 
    {
        return FT_Done_FreeType(hLibrary);
    }

    FONTMANAGER_API int PG_Face_New( FT_Library pLibrary,
                                     char *     pcFilePath,
                                     FT_Face *  aface) 
    {
        FT_Face face;
        int error = FT_New_Face( pLibrary, pcFilePath, 0, &face );
        *aface = face;
        return error;
    }

    FONTMANAGER_API int PG_Face_Done(FT_Face pFace) {
        return FT_Done_Face( pFace );
    }

    FONTMANAGER_API int PG_Face_SetCharSize( FT_Face face,
        unsigned long  char_width,
        unsigned long  char_height,
        unsigned int   horz_resolution,
        unsigned int   vert_resolution )
    {
        return FT_Set_Char_Size( face, char_width, char_height, horz_resolution, vert_resolution );
    }

    FONTMANAGER_API int PG_Set_Pixel_Sizes( FT_Face face, unsigned int x, unsigned int y ) {
        return FT_Set_Pixel_Sizes( face, x, y );
    }

    FONTMANAGER_API unsigned long PG_Face_GetCharIndex(FT_Face face, DWORD32 ulCodePoint) {
        return FT_Get_Char_Index( face, ulCodePoint );
    }

    FONTMANAGER_API int PG_Face_GenerateGlyph(FT_Face face, UINT16 uiMode, DWORD32 uiGlyphIndex) {
        FT_Error       error;
        FT_Render_Mode ftMode = (FT_Render_Mode)uiMode;

        error = FT_Load_Glyph( face, uiGlyphIndex, FT_LOAD_DEFAULT ); // FT_LOAD_DEFAULT
        if (error) return 5;

        error = FT_Render_Glyph( face->glyph, ftMode );
        if (error) return 6;

        return 0;
    }

    FONTMANAGER_API int PG_Face_CurrentGlyphMapData(FT_Face face, FTGlyphPos* pGlyphPos, FTGlyphBmp * pGlyphBmp ) {
        FT_GlyphSlot glyph = face->glyph;

        pGlyphBmp->width      = glyph->bitmap.width;
        pGlyphBmp->rows       = glyph->bitmap.rows;
        pGlyphBmp->pitch      = glyph->bitmap.pitch;
        pGlyphBmp->num_grays  = glyph->bitmap.num_grays;
        pGlyphBmp->pixel_mode = glyph->bitmap.pixel_mode;
        pGlyphBmp->bits       = glyph->bitmap.buffer;

        pGlyphPos->left      = glyph->bitmap_left;
        pGlyphPos->top       = glyph->bitmap_top;
        pGlyphPos->advance_x = glyph->advance.x;
        pGlyphPos->advance_y = glyph->advance.y;
        pGlyphPos->delta_lsb = glyph->lsb_delta;
        pGlyphPos->delta_rsb = glyph->rsb_delta;

        return 0;
    }

    /*
        FT_KERNING_DEFAULT  Return grid-fitted kerning distances in 26.6 fractional pixels.
        FT_KERNING_UNFITTED Return un-grid-fitted kerning distances in 26.6 fractional pixels.
        FT_KERNING_UNSCALED Return the kerning vector in original font units.   
    */

    FONTMANAGER_API int PG_Get_Kerning(
        FT_Face     face,
        DWORD32     left_glyph,
        DWORD32     right_glyph,
        DWORD32     kern_mode,
        INT32 *     iX,
        INT32 *     iY )
    {
        FT_Vector pntKern; 

        FT_Error error = FT_Get_Kerning(face, left_glyph, right_glyph, kern_mode, &pntKern);
        if( error ) return 7;

        *iX = pntKern.x;
        *iY = pntKern.y;

        return 0;
    }

}

